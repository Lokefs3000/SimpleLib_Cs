using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.GUI.sIMGUI
{
    internal class GuiFont
    {
        private Dictionary<byte, GuiGlyph> _glyphs = new Dictionary<byte, GuiGlyph>();

        public GuiFont(Stream source, Vector2 atlasDimensions)
        {
            using (BinaryReader br = new BinaryReader(source, Encoding.UTF8, true))
            {
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    GuiGlyph glyph = new GuiGlyph();

                    byte characterIndex = br.ReadByte();

                    glyph.Size = new Vector2(br.ReadInt16(), br.ReadInt16());
                    glyph.Bearing = new Vector2(br.ReadInt16(), br.ReadInt16());
                    glyph.Advance = br.ReadSingle(); glyph.Advance = glyph.Size.X + 3.0f;
                    
                    Vector2 uvMin = new Vector2(br.ReadInt32(), br.ReadInt32()) / atlasDimensions;
                    Vector2 uvMax = glyph.Size / atlasDimensions + uvMin;

                    glyph.UV = new Vector4(uvMin.X, uvMin.Y, uvMax.X, uvMax.Y);
                    glyph.Size *= 0.5f;
                    glyph.Bearing *= 0.5f;
                    glyph.Advance *= 0.5f;

                    if (characterIndex == (byte)' ')
                    {
                        glyph.Advance = 6.0f;
                    }

                    _glyphs[characterIndex] = glyph;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetGlyph(byte glyph, out GuiGlyph data)
        {
            return _glyphs.TryGetValue(glyph, out data);
        }
    }

    internal struct GuiGlyph
    {
        public Vector2 Size;
        public Vector2 Bearing;

        public float Advance;

        public Vector4 UV;
    }
}
