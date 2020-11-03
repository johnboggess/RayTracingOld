using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;

using SixLabors;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Processing;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Mathematics;

//Basic shader setup based off https://github.com/tgsstdio/OpenTK-Demos/blob/master/ComputeDemo/Demo.cs
namespace RayTracing
{
    public class Shader
	{
		public int WindowWidth;
		public int WindowHeight;
		public int LocalSizeX;
		public int LocalSizeY;

		private float speed = .1f;
		private Vector2 lastMousePosition;

		private int _renderProgram;
		private int _computeProgram;
        private int _renderTexture;
        private int _backgroundTexture;

		private int _backgroundWidth;
		private int _backgroundHeight;

		private Camera Camera = new Camera(1f, 16f / 9f, 1);
		private Window _window;

		public Shader(int localSizeX, int localSizeY, int windowWidth, int windowHeight, Window window)
		{
			LocalSizeX = localSizeX;
			LocalSizeY = localSizeY;
			WindowWidth = windowWidth;
			WindowHeight = windowHeight;
			_window = window;
        }

		public void Initialize()
        {
            GenerateTextures();
			_renderProgram = SetupRenderProgram();
			_computeProgram = SetupComputeProgram();

			MouseState ms = _window.MouseState;
			lastMousePosition.X = ms.X;
			lastMousePosition.Y = ms.Y;
		}

		public void Update()
		{
			Vector3 vel = new Vector3(0,0,0);
			KeyboardState ks = _window.KeyboardState;
			if (ks.IsKeyDown(Keys.A))
				vel += -Camera.Transform.Right;
			if (ks.IsKeyDown(Keys.D))
				vel += Camera.Transform.Right;
			if (ks.IsKeyDown(Keys.S))
				vel += -Camera.Transform.Forward;
			if (ks.IsKeyDown(Keys.W))
				vel += Camera.Transform.Forward;

			if(vel.LengthSquared > 0)
				vel = vel.Normalized() * speed;
			Camera.Transform.Position += vel;

			MouseState ms = _window.MouseState;
			float diffX = (ms.X - lastMousePosition.X) / 16;
			float diffY = (ms.Y - lastMousePosition.Y) / 16;
			lastMousePosition.X = ms.X;
			lastMousePosition.Y = ms.Y;

			Camera.Transform.Rotation = Quaternion.FromAxisAngle(Camera.Transform.Right, diffY) * Camera.Transform.Rotation;
			Camera.Transform.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, diffX) * Camera.Transform.Rotation;

			Matrix4 m = Camera.Transform.GetMatrix();
			GL.UseProgram(_computeProgram);
			GL.UniformMatrix4(GL.GetUniformLocation(_computeProgram, "ToWorldSpace"), true, ref m);
			GL.Uniform3(GL.GetUniformLocation(_computeProgram, "CameraPos"), Camera.Transform.Position);
			GL.Uniform1(GL.GetUniformLocation(_computeProgram, "ViewPortWidth"), Camera.ViewPortWidth);
			GL.Uniform1(GL.GetUniformLocation(_computeProgram, "ViewPortHeight"), Camera.ViewPortHeight);
			
			GL.DispatchCompute(WindowWidth / LocalSizeX, WindowHeight / LocalSizeY, 1);
		}

		public void Draw()
		{
			GL.UseProgram(_renderProgram);
			GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);
		}

		private int SetupRenderProgram()
		{
			int progHandle = GL.CreateProgram();
			int vp = GL.CreateShader(ShaderType.VertexShader);
			int fp = GL.CreateShader(ShaderType.FragmentShader);

			string vpSrc =
			"#version 430\n" +
			"in vec2 pos; " +
			"out vec2 texCoord; " +
			"void main() { " +
				"texCoord = pos*0.5f + 0.5f; " +
				"gl_Position = vec4(pos.x, pos.y, 0.0, 1.0); " +
			"} ";


			string fpSrc =
				"#version 430\n" +
				"uniform sampler2D srcTex; " +
				"in vec2 texCoord; " +
				"out vec4 color; " +
				"void main() { " +
				"color = texture(srcTex, texCoord); " +
				"} ";


			GL.ShaderSource(vp, vpSrc);
			GL.ShaderSource(fp, fpSrc);

			GL.CompileShader(vp);
			string err = GL.GetShaderInfoLog(vp);
			if (err == "")
				Console.WriteLine(err);
			GL.AttachShader(progHandle, vp);

			GL.CompileShader(fp);
			err = GL.GetShaderInfoLog(fp);
			if (err != "")
				Console.WriteLine(err);
			GL.AttachShader(progHandle, fp);

			GL.BindFragDataLocation(progHandle, 0, "color");
			GL.LinkProgram(progHandle);

			err = GL.GetProgramInfoLog(progHandle);
			if (err != "")
				Console.WriteLine(err);

			GL.UseProgram(progHandle);
			GL.Uniform1(GL.GetUniformLocation(progHandle, "srcTex"), 0);

			int vertArray;
			vertArray = GL.GenVertexArray();
			GL.BindVertexArray(vertArray);

			int posBuf;
			posBuf = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, posBuf);
			float[] data = {
				-1.0f, -1.0f,
				-1.0f, 1.0f,
				1.0f, -1.0f,
				1.0f, 1.0f
			};
			IntPtr dataSize = (IntPtr)(sizeof(float) * 8);

			GL.BufferData<float>(BufferTarget.ArrayBuffer, dataSize, data, BufferUsageHint.StreamDraw);
			int posPtr = GL.GetAttribLocation(progHandle, "pos");
			GL.VertexAttribPointer(posPtr, 2, VertexAttribPointerType.Float, false, 0, 0);
			GL.EnableVertexAttribArray(posPtr);

			return progHandle;
		}

		private void GenerateTextures()
		{
			_renderTexture = GL.GenTexture();

			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, _renderTexture);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, WindowWidth, WindowHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

			GL.BindImageTexture(0, _renderTexture, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba8);
			

			SixLabors.ImageSharp.Image<Rgba32> bck = SixLabors.ImageSharp.Image.Load<Rgba32>("Background.png");
			_backgroundWidth = bck.Width;
			_backgroundHeight = bck.Height;
			bck.Mutate(x => x.Flip(FlipMode.Vertical));
			List<byte> pixels = new List<byte>();
			for(int r = 0; r < bck.Height; r++)
            {
				Span<Rgba32> span = bck.GetPixelRowSpan(r);
				for(int c = 0; c < bck.Width; c++)
                {
					pixels.Add(span[c].R);
					pixels.Add(span[c].G);
					pixels.Add(span[c].B);
					pixels.Add(span[c].A);
				}
            }

			_backgroundTexture = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, _backgroundTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bck.Width, bck.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.ToArray());

            GL.BindImageTexture(1, _backgroundTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.Rgba8);
        }

		private int SetupComputeProgram()
		{
			int progHandle = GL.CreateProgram();
			int computeShader = GL.CreateShader(ShaderType.ComputeShader);

			string src = File.ReadAllText("Shader.glsl");
			src = replaceVariables(src);

			GL.ShaderSource(computeShader, src);
			GL.CompileShader(computeShader);

			string err = GL.GetShaderInfoLog(computeShader);
			if (err != "")
				Console.WriteLine(err);

			GL.AttachShader(progHandle, computeShader);

			GL.LinkProgram(progHandle);
			err = GL.GetProgramInfoLog(progHandle);
			if (err != "")
				Console.WriteLine(err);	

			GL.UseProgram(progHandle);

			GL.Uniform1(GL.GetUniformLocation(progHandle, "destTex"), 0);
            GL.Uniform1(GL.GetUniformLocation(progHandle, "backgroundTex"), 1);
			GL.Uniform1(GL.GetUniformLocation(progHandle, "BackgroundWidth"), _backgroundWidth);
			GL.Uniform1(GL.GetUniformLocation(progHandle, "BackgroundHeight"), _backgroundHeight);

			return progHandle;
		}


		private string replaceVariables(string src)
        {
			src = src.Replace("$WindowWidth", WindowWidth.ToString());
			src = src.Replace("$WindowHeight", WindowHeight.ToString());
			src = src.Replace("$LocalSizeX", LocalSizeX.ToString());
			src = src.Replace("$LocalSizeY", LocalSizeY.ToString());
			return src;
		}
	}		
}			
			
			