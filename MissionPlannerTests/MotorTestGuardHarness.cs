using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

internal static class MotorTestGuardHarness
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Type runnerType, commandType;
    private static object runner;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static object Begin() => Call(runner, "TryBegin");
    private static void End(object operation) => ((IDisposable)operation).Dispose();
    private static int Field(object command, string name) => (int)commandType.GetField(name, Flags).GetValue(command);
    private static object Command(int motor, int speed = 5, int duration = 2, int count = 0) =>
        Activator.CreateInstance(commandType, Flags, null, new object[] {motor, speed, duration, count}, null);

    private static Task Execute(object operation, object[] commands, Func<object, Task<bool>> send, Func<bool> sameTarget = null)
    {
        var method = runnerType.GetMethod("ExecuteAsync", Flags);
        var senderType = method.GetParameters()[3].ParameterType;
        var parameter = Expression.Parameter(commandType);
        var sender = Expression.Lambda(senderType, Expression.Invoke(Expression.Constant(send), Expression.Convert(parameter, typeof(object))), parameter).Compile();
        var array = Array.CreateInstance(commandType, commands.Length);
        for (int i = 0; i < commands.Length; i++) array.SetValue(commands[i], i);
        return (Task)method.Invoke(null, new object[] {operation, array, 4, sender, sameTarget ?? (() => true)});
    }

    private static async Task Run()
    {
        var operation = Begin();
        var pending = new TaskCompletionSource<bool>();
        var sent = new List<string>();
        var task = Execute(operation, new[] {Command(1), Command(2), Command(3), Command(4)}, command =>
        {
            sent.Add(Field(command, "Motor") + ":" + Field(command, "Speed"));
            return sent.Count == 1 ? pending.Task : Task.FromResult(true);
        });
        Check(sent.SequenceEqual(new[] {"1:5"}) && !task.IsCompleted, "First request did not wait asynchronously");
        for (int i = 0; i < 1000; i++) Check(Begin() == null, "Repeated start entered while ACK pending");
        for (int i = 0; i < 1000; i++) Check((bool)Call(runner, "RequestStop"), "Stop request lost");
        Check(sent.Count == 1, "Stop raced the pending motor ACK");
        pending.SetResult(true);
        await task;
        Check(sent.SequenceEqual(new[] {"1:5", "1:0", "2:0", "3:0", "4:0"}), "Stop duplicated or more starts sent after stop");
        End(operation);

        foreach (var timeout in new[] {false, true})
        {
            operation = Begin(); int calls = 0; bool failed = false;
            try
            {
                await Execute(operation, new[] {Command(1), Command(2)}, command =>
                {
                    calls++;
                    if (timeout) throw new TimeoutException("simulated ACK timeout");
                    return Task.FromResult(false);
                });
            }
            catch (Exception ex) { failed = timeout ? ex is TimeoutException : ex is InvalidOperationException; }
            finally { End(operation); }
            Check(failed && calls == 1, "Failure sent a remaining start or disappeared");
            var next = Begin(); Check(next != null, "Failure left the gate locked");
            End(operation); Check(Begin() == null, "Old completion released a newer operation"); End(next);
        }

        foreach (var changeTarget in new[] {false, true})
        {
            operation = Begin(); pending = new TaskCompletionSource<bool>(); int calls = 0; bool same = true;
            task = Execute(operation, new[] {Command(1), Command(2)}, command => { calls++; return pending.Task; }, () => same);
            if (changeTarget) same = false; else Call(operation, "CancelPendingStarts");
            pending.SetResult(true);
            bool failed = false;
            try { await task; } catch (InvalidOperationException) { failed = true; }
            finally { End(operation); }
            Check(calls == 1 && failed == changeTarget, "Page cancellation/target switch sent a stale start");
        }

        operation = Begin(); Call(runner, "RequestStop"); int stopCalls = 0; bool stopFailed = false;
        try
        {
            await Execute(operation, new[] {Command(1)}, command =>
            {
                Check(Field(command, "Speed") == 0 && Field(command, "Duration") == 0, "Stop carried nonzero throttle/duration");
                stopCalls++; return Task.FromResult(stopCalls != 1);
            });
        }
        catch (InvalidOperationException) { stopFailed = true; }
        finally { End(operation); }
        Check(stopFailed && stopCalls == 4, "One denied stop prevented attempts for remaining motors");

        operation = Begin(); Call(runner, "RequestStop"); stopCalls = 0;
        try { await Execute(operation, new object[0], c => { stopCalls++; return Task.FromResult(true); }, () => false); }
        catch (InvalidOperationException) { }
        finally { End(operation); }
        Check(stopCalls == 0, "Stop redirected to changed/disconnected target");
        Check(!(bool)runnerType.GetProperty("IsBusy", Flags).GetValue(runner), "Runner did not return to idle");
    }

    private static int Main(string[] args)
    {
        var assembly = Assembly.LoadFrom(args[0]);
        runnerType = assembly.GetType("MissionPlanner.FMT.FmtMotorTestRunner", true);
        commandType = runnerType.GetNestedType("Command", BindingFlags.NonPublic);
        runner = Activator.CreateInstance(runnerType, true);
        Run().GetAwaiter().GetResult();
        Console.WriteLine("PASS: compiled runner; 1000 repeated starts rejected; 1000 stops coalesced; pending ACK serialization; stop priority; denied/timeout recovery; stale-owner isolation; page cancellation; target/disconnect guards; stop-failure continuation; no transport opened");
        return 0;
    }
}
