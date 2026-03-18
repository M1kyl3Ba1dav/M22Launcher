using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GameLauncher
{
    public class ControllerManager
    {
        [StructLayout(LayoutKind.Sequential)]
        struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [DllImport("xinput1_4.dll")]
        static extern int XInputGetState(int dwUserIndex, ref XINPUT_STATE pState);

        public event Action OnUp;
        public event Action OnDown;
        public event Action OnSelect;

        private bool running = true;

        public void Start()
        {
            Task.Run(() =>
            {
                XINPUT_STATE state = new XINPUT_STATE();

                while (running)
                {
                    XInputGetState(0, ref state);

                    ushort buttons = state.Gamepad.wButtons;

                    // D-PAD UP
                    if ((buttons & 0x0001) != 0)
                        OnUp?.Invoke();

                    // D-PAD DOWN
                    if ((buttons & 0x0002) != 0)
                        OnDown?.Invoke();

                    // A BUTTON
                    if ((buttons & 0x1000) != 0)
                        OnSelect?.Invoke();

                    Thread.Sleep(200); // prevents spam
                }
            });
        }

        public void Stop()
        {
            running = false;
        }

    }
}
