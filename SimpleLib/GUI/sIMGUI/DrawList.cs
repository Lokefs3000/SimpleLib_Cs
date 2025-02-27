using SimpleLib.Resources.Data;
using SimpleLib.Utility;
using SimpleRHI;
using System.Numerics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace SimpleLib.GUI.sIMGUI
{
    public class DrawList : IDisposable
    {
        private UnsafeList<sIMGUIVertex> _vertices;
        private UnsafeList<ushort> _indices;

        private List<sIMGUIDrawCmd> _commands;
        private Texture? _activeTextureView;
        private ushort _prevIndexCount;

        private Stack<Vector4> _clipStack;
        private bool _cmdDirty;

        public DrawList()
        {
            _vertices = new UnsafeList<sIMGUIVertex>(32 * 4) { Name = "sIMGUIDrawList" };
            _indices = new UnsafeList<ushort>(32 * 6) { Name = "sIMGUIDrawList" };

            _commands = new List<sIMGUIDrawCmd>();
            _activeTextureView = null;
            _prevIndexCount = 0;

            _clipStack = new Stack<Vector4>(8);
            _cmdDirty = true;
        }

        public void Dispose()
        {
            _vertices.Dispose();
            _indices.Dispose();
        }

        internal void Reset()
        {
            _vertices.Clear();
            _indices.Clear();
            _commands.Clear();
            _clipStack.Clear();

            _activeTextureView = null;
            _prevIndexCount = 0;
            _cmdDirty = true;
        }

        internal void End()
        {
            _cmdDirty = true;
            ValidateCmd(sIMGUI.Context.GlobalTexture);
        }

        public void PushClip(Vector4 clip)
        {
            _clipStack.Push(clip);
            _cmdDirty = true;
        }

        public void PopClip()
        {
            _clipStack.TryPop(out Vector4 _);
            _cmdDirty = true;
        }

        public void ValidateCmd(Texture texture)
        {
            if (_cmdDirty || _activeTextureView != texture)
            {
                if (_commands.Count > 0)
                {
                    Span<sIMGUIDrawCmd> span = CollectionsMarshal.AsSpan(_commands);
                    ref sIMGUIDrawCmd last = ref span[span.Length - 1];
                    last.IndexCount = (ushort)(_indices.Count - _prevIndexCount);
                }

                _commands.Add(new sIMGUIDrawCmd((ushort)_vertices.Count, (ushort)_indices.Count, 0, _clipStack.Count > 0 ? _clipStack.Peek() : null, texture));
                _prevIndexCount = (ushort)_indices.Count;
                _activeTextureView = texture;
                _cmdDirty = false;
            }
        }

        public void AddLine(Vector2 min, Vector2 max, Vector4 color, float thickness = 1.0f)
        {
            //drawing accurate lines correct sucks >:(

            Vector2 fwd = Vector2.Normalize(max - min);
            Vector2 rgt = new Vector2(-fwd.Y, fwd.X) * thickness;

            min -= fwd;
            max += fwd;

            _vertices.Ensure(6);
            _indices.Ensure(4);

            int initial = (int)_vertices.Count;

            _vertices.AddNoResize(new sIMGUIVertex(min - rgt, Vector2.Zero, color));
            _vertices.AddNoResize(new sIMGUIVertex(min + rgt, Vector2.Zero, color));
            _vertices.AddNoResize(new sIMGUIVertex(max - rgt, Vector2.Zero, color));
            _vertices.AddNoResize(new sIMGUIVertex(max + rgt, Vector2.Zero, color));

            _indices.AddNoResize((ushort)(    initial));
            _indices.AddNoResize((ushort)(1 + initial));
            _indices.AddNoResize((ushort)(2 + initial));

            _indices.AddNoResize((ushort)(1 + initial));
            _indices.AddNoResize((ushort)(3 + initial));
            _indices.AddNoResize((ushort)(2 + initial));
        }

        public void AddRect(Vector2 min, Vector2 max, Vector4 color, float thickness = 1.0f)
        {
            ValidateCmd(sIMGUI.Context.GlobalTexture);

            min.Y = -min.Y;
            max.Y = -max.Y;

            AddLine(new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), color, thickness);
            AddLine(new Vector2(min.X, max.Y), new Vector2(max.X, max.Y), color, thickness);
            AddLine(new Vector2(min.X, min.Y), new Vector2(min.X, max.Y), color, thickness);
            AddLine(new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), color, thickness);
        }

        public void AddRectFilled(Vector2 min, Vector2 max, Vector4 color)
        {
            ValidateCmd(sIMGUI.Context.GlobalTexture);

            min.Y = -min.Y;
            max.Y = -max.Y;

            _vertices.Ensure(6);
            _indices.Ensure(4);

            int initial = (int)_vertices.Count;

            _vertices.AddNoResize(new sIMGUIVertex(new Vector2(min.X, min.Y), new Vector2(0.0f, 0.99f), color));
            _vertices.AddNoResize(new sIMGUIVertex(new Vector2(min.X, max.Y), new Vector2(0.0f, 0.0f), color));
            _vertices.AddNoResize(new sIMGUIVertex(new Vector2(max.X, min.Y), new Vector2(1.0f, 0.99f), color));
            _vertices.AddNoResize(new sIMGUIVertex(new Vector2(max.X, max.Y), new Vector2(1.0f, 0.0f), color));

            _indices.AddNoResize((ushort)(initial));
            _indices.AddNoResize((ushort)(1 + initial));
            _indices.AddNoResize((ushort)(2 + initial));

            _indices.AddNoResize((ushort)(1 + initial));
            _indices.AddNoResize((ushort)(3 + initial));
            _indices.AddNoResize((ushort)(2 + initial));
        }

        public void AddText(Vector2 pos, string text, Vector4 color)
        {
            ValidateCmd(sIMGUI.Context.GlobalTexture);

            pos.Y = -pos.Y;

            //naive
            _vertices.Ensure((uint)(text.Length * 4));
            _indices.Ensure((uint)(text.Length * 6));

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (sIMGUI.Context.GlobalFont.TryGetGlyph((byte)c, out GuiGlyph data))
                {
                    Vector2 min = new Vector2(pos.X + data.Bearing.X, pos.Y - (data.Size.Y - data.Bearing.Y));
                    Vector2 max = min + data.Size;

                    int initial = (int)_vertices.Count;

                    _vertices.AddNoResize(new sIMGUIVertex(new Vector2(min.X, min.Y), new Vector2(data.UV.X, data.UV.W), color));
                    _vertices.AddNoResize(new sIMGUIVertex(new Vector2(min.X, max.Y), new Vector2(data.UV.X, data.UV.Y), color));
                    _vertices.AddNoResize(new sIMGUIVertex(new Vector2(max.X, min.Y), new Vector2(data.UV.Z, data.UV.W), color));
                    _vertices.AddNoResize(new sIMGUIVertex(new Vector2(max.X, max.Y), new Vector2(data.UV.Z, data.UV.Y), color));

                    _indices.AddNoResize((ushort)(initial));
                    _indices.AddNoResize((ushort)(1 + initial));
                    _indices.AddNoResize((ushort)(2 + initial));

                    _indices.AddNoResize((ushort)(1 + initial));
                    _indices.AddNoResize((ushort)(3 + initial));
                    _indices.AddNoResize((ushort)(2 + initial));

                    pos.X += data.Advance;
                }
            }
        }

        public Span<sIMGUIVertex> Vertices => _vertices.AsSpan();
        public Span<ushort> Indices => _indices.AsSpan();
        public Span<sIMGUIDrawCmd> DrawCommands => CollectionsMarshal.AsSpan(_commands);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct sIMGUIVertex
    {
        public readonly Vector2 Position;
        public readonly Vector2 UV;
        public readonly Vector4 Color;

        public sIMGUIVertex(Vector2 position, Vector2 uv, Vector4 color)
        {
            Position = position;
            UV = uv;
            Color = color;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct sIMGUIDrawCmd
    {
        public readonly ushort VertexOffset;

        public readonly ushort IndexOffset;
        public ushort IndexCount;

        public readonly Vector4? Clip;
        public readonly Texture Texture;
        
        public sIMGUIDrawCmd(ushort vOffset, ushort iOffset, ushort iCount, Vector4? clip, Texture texture)
        {
            VertexOffset = vOffset;
            IndexOffset = iOffset;
            IndexCount = iCount;
            Clip = clip;
            Texture = texture;
        }
    }
}
