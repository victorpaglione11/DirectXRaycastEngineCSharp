using System;
using System.Diagnostics;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Windows;
using Factory2D = SharpDX.Direct2D1.Factory;

namespace RaycasterDirectX
{
    class Program
    {
        [STAThread]
        static void Main()
        {
            // O mapa clássico
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

            // Variáveis do jogador
            float playerX = 3f;
            float playerY = 3f;
            float playerAngle = 0f;

            float FOV = MathF.PI / 4f;
            float Depth = 16f;

            // Resolução da janela (bem maior que a do Console)
            const int ScreenWidth = 800;
            const int ScreenHeight = 600;

            // Criação da janela do SharpDX
            RenderForm form = new RenderForm("DirectX Raycaster (SharpDX)")
            {
                ClientSize = new System.Drawing.Size(ScreenWidth, ScreenHeight)
            };

            // Controle suave de input
            bool turnLeft = false;
            bool turnRight = false;

            form.KeyDown += (sender, e) => {
                if (e.KeyCode == Keys.A) turnLeft = true;
                if (e.KeyCode == Keys.D) turnRight = true;
            };
            form.KeyUp += (sender, e) => {
                if (e.KeyCode == Keys.A) turnLeft = false;
                if (e.KeyCode == Keys.D) turnRight = false;
            };

            // Inicialização do Direct2D
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

            // Pincéis (Brushes) para desenhar
            SolidColorBrush wallBrush = new SolidColorBrush(renderTarget, Color.White);
            SolidColorBrush ceilingBrush = new SolidColorBrush(renderTarget, Color.DarkSlateGray);
            SolidColorBrush floorBrush = new SolidColorBrush(renderTarget, Color.SaddleBrown);

            Stopwatch timer = new Stopwatch();
            timer.Start();
            double lastTime = 0;

            // Loop principal renderizado pela placa de vídeo
            RenderLoop.Run(form, () =>
            {
                double currentTime = timer.Elapsed.TotalSeconds;
                double elapsedTime = currentTime - lastTime;
                lastTime = currentTime;

                // Movimento baseado em tempo real (Delta Time) e não em repetição de tecla
                if (turnLeft) playerAngle -= 2.0f * (float)elapsedTime;
                if (turnRight) playerAngle += 2.0f * (float)elapsedTime;

                // Inicia o desenho na GPU
                renderTarget.BeginDraw();
                renderTarget.Clear(Color.Black);

                // Desenha teto e chão preenchendo metades da tela
                renderTarget.FillRectangle(new RectangleF(0, 0, ScreenWidth, ScreenHeight / 2f), ceilingBrush);
                renderTarget.FillRectangle(new RectangleF(0, ScreenHeight / 2f, ScreenWidth, ScreenHeight / 2f), floorBrush);

                // Dispara um raio para cada coluna de pixels da tela
                for (int x = 0; x < ScreenWidth; x++)
                {
                    float rayAngle = playerAngle - (FOV / 2f) + ((float)x / ScreenWidth) * FOV;

                    float distance = 0f;
                    bool hitWall = false;

                    float eyeX = MathF.Sin(rayAngle);
                    float eyeY = MathF.Cos(rayAngle);

                    // Lógica de Raycasting DDA (Avanço passo a passo)
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

                    // Correção do efeito "Olho de Peixe" (Fisheye)
                    float correctedDistance = distance * MathF.Cos(rayAngle - playerAngle);
                    if (correctedDistance < 0.1f) correctedDistance = 0.1f;

                    // Cálculo da altura da parede
                    int ceiling = (int)((ScreenHeight / 2.0f) - ScreenHeight / correctedDistance);
                    int floor = ScreenHeight - ceiling;

                    // Sombreamento contínuo baseado na distância (mais perto = mais branco, longe = mais preto)
                    float shadeIntensity = 1.0f - (correctedDistance / Depth);
                    if (shadeIntensity < 0) shadeIntensity = 0;

                    wallBrush.Color = new Color4(shadeIntensity, shadeIntensity, shadeIntensity, 1.0f);

                    // Desenha a fatia vertical da parede
                    renderTarget.DrawLine(
                        new Vector2(x, ceiling),
                        new Vector2(x, floor),
                        wallBrush,
                        1.0f // Espessura da linha (1 pixel)
                    );
                }

                // Finaliza o desenho na GPU
                renderTarget.EndDraw();

                // Atualiza o FPS no título da janela
                form.Text = $"DirectX Raycaster - FPS: {(int)(1.0 / elapsedTime)}";
            });

            // Limpeza de recursos não gerenciados ao fechar
            wallBrush.Dispose();
            ceilingBrush.Dispose();
            floorBrush.Dispose();
            renderTarget.Dispose();
            factory.Dispose();
        }
    }
}