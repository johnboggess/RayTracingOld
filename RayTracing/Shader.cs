using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;

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
		private OpenTK.Vector2 lastMousePosition;

		private int _renderProgram;
		private int _computeProgram;

		private Camera Camera = new Camera(1f, 16f / 9f, 1);

		public Shader(int localSizeX, int localSizeY, int windowWidth, int windowHeight)
		{
			LocalSizeX = localSizeX;
			LocalSizeY = localSizeY;
			WindowWidth = windowWidth;
			WindowHeight = windowHeight;
        }

		public void Initialize()
		{
			int texHandle = GenerateDestTex();
			_renderProgram = SetupRenderProgram(texHandle);
			_computeProgram = SetupComputeProgram(texHandle);

			MouseState ms = Mouse.GetCursorState();
			lastMousePosition.X = ms.X;
			lastMousePosition.Y = ms.Y;
		}

		public void Update()
		{
			OpenTK.Vector3 vel = new OpenTK.Vector3(0,0,0);
			KeyboardState ks = Keyboard.GetState();
			if (ks.IsKeyDown(Key.A))
				vel += -Camera.Transform.Right;
			if (ks.IsKeyDown(Key.D))
				vel += Camera.Transform.Right;
			if (ks.IsKeyDown(Key.S))
				vel += -Camera.Transform.Forward;
			if (ks.IsKeyDown(Key.W))
				vel += Camera.Transform.Forward;

			if(vel.LengthSquared > 0)
				vel = vel.Normalized() * speed;
			Camera.Transform.Position += vel;

			MouseState ms = Mouse.GetCursorState();
			float diffX = (ms.X - lastMousePosition.X) / 16;
			float diffY = (ms.Y - lastMousePosition.Y) / 16;
			lastMousePosition.X = ms.X;
			lastMousePosition.Y = ms.Y;

			Camera.Transform.Rotation = OpenTK.Quaternion.FromAxisAngle(Camera.Transform.Right, diffY) * Camera.Transform.Rotation;
			Camera.Transform.Rotation = OpenTK.Quaternion.FromAxisAngle(OpenTK.Vector3.UnitY, diffX) * Camera.Transform.Rotation;

			OpenTK.Matrix4 m = Camera.Transform.GetMatrix();
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

		private int SetupRenderProgram(int texHandle)
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

		private int GenerateDestTex()
		{
			int texHandle;
			texHandle = GL.GenTexture();

			GL.ActiveTexture(TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, texHandle);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);
			GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
			GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, WindowWidth, WindowHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

			GL.BindImageTexture(0, texHandle, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.Rgba8);

			return texHandle;
		}

		private int SetupComputeProgram(int texHandle)
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
			
			