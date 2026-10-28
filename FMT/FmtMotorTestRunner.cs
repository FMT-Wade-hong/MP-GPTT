using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.FMT
{
    // One owner across page instances. Repeated starts are rejected, never queued.
    internal sealed class FmtMotorTestRunner
    {
        private readonly object sync = new object();
        private Operation active;
        internal bool IsBusy { get { lock (sync) return active != null; } }
        internal bool StopRequested { get { lock (sync) return active?.StopRequested == true; } }

        internal Operation TryBegin()
        {
            lock (sync)
            {
                if (active != null) return null;
                return active = new Operation(this);
            }
        }

        internal bool RequestStop()
        {
            lock (sync)
            {
                if (active == null) return false;
                active.RequestStop();
                return true;
            }
        }

        internal sealed class Operation : IDisposable
        {
            private readonly FmtMotorTestRunner owner;
            private int stop, cancelled;
            internal Operation(FmtMotorTestRunner owner) { this.owner = owner; }
            internal bool StopRequested => Volatile.Read(ref stop) != 0;
            internal bool Cancelled => Volatile.Read(ref cancelled) != 0;
            internal void RequestStop() { Interlocked.Exchange(ref stop, 1); }
            internal void CancelPendingStarts() { Interlocked.Exchange(ref cancelled, 1); }
            public void Dispose()
            {
                lock (owner.sync)
                    if (ReferenceEquals(owner.active, this)) owner.active = null;
            }
        }

        internal sealed class Command
        {
            internal int Motor, Speed, Duration, Count;
            internal Command(int motor, int speed, int duration, int count = 0)
            { Motor = motor; Speed = speed; Duration = duration; Count = count; }
        }

        internal static async Task ExecuteAsync(Operation operation, IEnumerable<Command> starts, int motorCount,
            Func<Command, Task<bool>> send, Func<bool> sameTarget)
        {
            Exception failure = null;
            try
            {
                foreach (var command in starts)
                {
                    if (operation.StopRequested || operation.Cancelled) break;
                    if (!sameTarget()) throw new InvalidOperationException("連線或目標已改變，未送出後續馬達測試。");
                    if (!await send(command))
                        throw new InvalidOperationException("飛控拒絕馬達 " + command.Motor + " 的測試命令。");
                }
            }
            catch (Exception ex) { failure = ex; }

            // Wait for the current command to settle before sending stops: all
            // motor ACKs share one command id and must not race each other.
            if (operation.StopRequested)
            {
                failure = null;
                for (var motor = 1; motor <= motorCount; motor++)
                {
                    if (!sameTarget())
                    {
                        failure = new InvalidOperationException("連線或目標已改變，無法確認全部停止。");
                        break;
                    }
                    try
                    {
                        if (!await send(new Command(motor, 0, 0)))
                            failure = new InvalidOperationException("飛控未接受所有馬達的停止命令。");
                    }
                    catch (Exception ex) { failure = ex; }
                }
            }
            if (failure != null) throw failure;
        }
    }
}
