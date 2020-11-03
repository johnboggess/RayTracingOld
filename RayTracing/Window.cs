using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;


namespace RayTracing
{
    public class Window : GameWindow
    {
        public float AspectRatio;

        Shader _rayTracerShader;

        public Window(int height, float aspectRatio, string title) : base(GameWindowSettings.Default, new NativeWindowSettings() { Size = new Vector2i((int)(height * aspectRatio), height), Title = title } )
        {
            Run();
        }

        protected override void OnLoad()
        {
            _rayTracerShader = new Shader(10, 10, Size.X, Size.Y, this);
            _rayTracerShader.Initialize();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            _rayTracerShader.Update();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _rayTracerShader.Draw();
            SwapBuffers();
        }
    }
}
