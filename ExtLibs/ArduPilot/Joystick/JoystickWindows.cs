using System;
using System.Collections.Generic;
using System.Linq;
using MissionPlanner.Utilities;
using SharpDX.DirectInput;

namespace MissionPlanner.Joystick
{
    public class JoystickWindows: JoystickBase, IDisposable
    {
        private SharpDX.DirectInput.Joystick joystick;
        private readonly object deviceLock = new object();

        public JoystickWindows(Func<MAVLinkInterface> func) : base(func)
        {
         
        }

        public override void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Implement reccomended best practice dispose pattern
        /// http://msdn.microsoft.com/en-us/library/b1yfkh5e%28v=vs.110%29.aspx
        /// </summary>
        /// <param name="disposing"></param>
        virtual protected void Dispose(bool disposing)
        {
            lock (deviceLock)
            {
            enabled = false;
            try
            {
                //not sure if this is a problem from the finalizer?
                if (disposing && joystick != null && joystick.Properties != null)
                    joystick.Unacquire();
            }
            catch
            {
            }

            try
            {
                if (disposing && joystick != null)
                    joystick.Dispose();
            }
            catch
            {
            }

            //tell gc not to call finalize, this object will be GC'd quicker now.
            GC.SuppressFinalize(this);
            joystick = null;
            }
        }

        public override bool AcquireJoystick(string name)
        {
            lock (deviceLock)
            {
            joystick = getJoyStickByNameInternal(name);

            if (joystick == null)
                return false;

            joystick.Acquire();

            joystick.Poll();
            LastInputError = null;
            return true;
            }
        }

        public override int getNumberPOV()
        {
            return joystick.Capabilities.PovCount;
        }

        public override bool IsJoystickValid()
        {
            return joystick != null && !joystick.IsDisposed;
        }

        public override IMyJoystickState GetCurrentState()
        {
            lock (deviceLock)
            {
            try
            {
            if (joystick == null || joystick.IsDisposed) throw new InvalidOperationException("搖桿裝置已釋放。");
            joystick.Poll();
            var snapshot = new WindowsJoystickState(joystick.GetCurrentState());
            return snapshot;
            }
            catch { LastInputError = "搖桿讀取中斷，請重新選擇裝置並啟用。"; throw; }
            }
        }

        public override void UnAcquireJoyStick()
        {
            lock (deviceLock)
            {
            if (joystick == null)
                return;
            joystick.Unacquire();
            }
        }

        public override int getNumButtons()
        {
            if (joystick == null)
                return 0;
            return joystick.Capabilities.ButtonCount;
        }

        internal new static IList<DeviceInstance> getDevices()
        {
            return new DirectInput().GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
        }
        internal static SharpDX.DirectInput.Joystick getJoyStickByNameInternal(string name)
        {
            var joysticklist = getDevices();

            foreach (DeviceInstance device in joysticklist)
            {
                if (device.ProductName.TrimUnPrintable() == name)
                {
                    return new SharpDX.DirectInput.Joystick(new DirectInput(), device.InstanceGuid);
                }
            }

            return null;
        }

        public new static JoystickBase getJoyStickByName(string name)
        {
            var joysticklist = getDevices();

            foreach (var device in joysticklist)
            {
                if (device.ProductName.TrimUnPrintable() == name)
                {
                    var js = new JoystickWindows(() => null);
                    js.AcquireJoystick(name);
                    return js;
                }
            }

            return null;
        }
    }
}
