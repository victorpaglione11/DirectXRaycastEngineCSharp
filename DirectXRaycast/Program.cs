using SharpDX;
using SharpDX.Direct2D1;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Color = SharpDX.Color;
using Factory2D = SharpDX.Direct2D1.Factory;
using Point = SharpDX.Point;
using RectangleF = SharpDX.RectangleF;
using Vector2 = SharpDX.Vector2;

namespace RaycasterDirectX
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParameter;
            public IntPtr LParameter;
            public uint Time;
            public Point Location;
        }

        [DllImport("user32.dll")]
        public static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

        static bool IsApplicationIdle()
        {
            return PeekMessage(out NativeMessage result, IntPtr.Zero, 0, 0, 0) == 0;
        }

        [STAThread]
        static void Main()
        {
            string[] map = {
                "##########",
                "#........#",
                "#........#",
                "#........#",
                "#........#",
                "##########"
            };

            const int MapWidth = 10;
            const int MapHeight = 6;

            float playerX = 3f;
            float playerY = 3f;
            float playerAngle = 0f;

            float FOV = MathF.PI / 4f;
            float Depth = 16f;

            const int ScreenWidth = 800;
            const int ScreenHeight = 600;

            Form form = new Form()
            {
                Text = "DirectX Raycaster",
                ClientSize = new Size(ScreenWidth, ScreenHeight),
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false,
                StartPosition = FormStartPosition.CenterScreen
            };

            bool turnLeft = false;
            bool turnRight = false;

            form.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.A)
                    turnLeft = true;
                if (e.KeyCode == Keys.D)
                    turnRight = true;
            };
            form.KeyUp += (sender, e) =>
            {
                if (e.KeyCode == Keys.A)
                    turnLeft = false;
                if (e.KeyCode == Keys.D)
                    turnRight = false;
            };

            Factory2D factory = new Factory2D();
            WindowRenderTarget renderTarget = new WindowRenderTarget(
                factory,
                new RenderTargetProperties(new PixelFormat(SharpDX.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Ignore)),
                new HwndRenderTargetProperties()
                {
                    Hwnd = form.Handle,
                    PixelSize = new Size2(ScreenWidth, ScreenHeight),
                    PresentOptions = PresentOptions.Immediately
                }
            );

            renderTarget.AntialiasMode = AntialiasMode.Aliased;

            SolidColorBrush wallBrush = new SolidColorBrush(renderTarget, Color.Red);
            SolidColorBrush ceilingBrush = new SolidColorBrush(renderTarget, Color.LightSkyBlue);
            SolidColorBrush floorBrush = new SolidColorBrush(renderTarget, Color.SlateGray);

            Stopwatch timer = new Stopwatch();
            timer.Start();
            double lastTime = 0;

            Application.Idle += delegate
            {
                while (IsApplicationIdle())
                {
                    Thread.Sleep(16);
                    double currentTime = timer.Elapsed.TotalSeconds;
                    double elapsedTime = currentTime - lastTime;
                    lastTime = currentTime;

                    if (turnLeft)
                        playerAngle -= 2.0f * (float)elapsedTime;
                    if (turnRight)
                        playerAngle += 2.0f * (float)elapsedTime;

                    renderTarget.BeginDraw();
                    renderTarget.Clear(Color.Black);

                    renderTarget.FillRectangle(new RectangleF(0, 0, ScreenWidth, ScreenHeight / 2f), ceilingBrush);
                    renderTarget.FillRectangle(new RectangleF(0, ScreenHeight / 2f, ScreenWidth, ScreenHeight / 2f), floorBrush);

                    for (int x = 0; x < ScreenWidth; x++)
                    {
                        float rayAngle = playerAngle - (FOV / 2f) + ((float)x / ScreenWidth) * FOV;

                        float distance = 0f;
                        bool hitWall = false;

                        float eyeX = MathF.Sin(rayAngle);
                        float eyeY = MathF.Cos(rayAngle);

                        while (!hitWall && distance < Depth)
                        {
                            distance += 0.015f;

                            int testX = (int)(playerX + eyeX * distance);
                            int testY = (int)(playerY + eyeY * distance);

                            if (testX < 0 || testX >= MapWidth || testY < 0 || testY >= MapHeight)
                            {
                                hitWall = true;
                                distance = Depth;
                            }
                            else if (map[testY][testX] == '#')
                            {
                                hitWall = true;
                            }
                        }

                        float correctedDistance = distance * MathF.Cos(rayAngle - playerAngle);
                        
                        if (correctedDistance < 0.1f) 
                            correctedDistance = 0.1f;

                        int ceiling = (int)((ScreenHeight / 2.0f) - ScreenHeight / correctedDistance);
                        int floor = ScreenHeight - ceiling;

                        float shadeIntensity = 1.0f - (correctedDistance / Depth);
                        if (shadeIntensity < 0) 
                            shadeIntensity = 0;

                        wallBrush.Color = new Color4(shadeIntensity, shadeIntensity, shadeIntensity, 1.0f);

                        renderTarget.DrawLine(new Vector2(x, ceiling),new Vector2(x, floor),wallBrush,1.0f);
                    }

                    renderTarget.EndDraw();

                    if (elapsedTime > 0)
                        form.Text = $"DirectX Raycaster - FPS: {(int)(1.0 / elapsedTime)}";
                }
            };

            Application.Run(form);

            wallBrush.Dispose();
            ceilingBrush.Dispose();
            floorBrush.Dispose();
            renderTarget.Dispose();
            factory.Dispose();
        }
    }
}