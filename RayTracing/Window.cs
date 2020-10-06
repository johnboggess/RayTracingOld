using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace RayTracing
{
    public class Window : GameWindow
    {
        public float AspectRatio;

        Shader _rayTracerShader;

        public Window(int height, float aspectRatio, string title) : base((int)(height*aspectRatio), height, GraphicsMode.Default, title)
        {
            Run(60f, 60f);
        }

        protected override void OnLoad(EventArgs e)
        {
            _rayTracerShader = new Shader(10, 10, Width, Height);
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
