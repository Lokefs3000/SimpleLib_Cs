using Hexa.NET.FreeType;
using System.Text;

namespace IMGUIFontAtlasGen
{
    internal class Program
    {
        static unsafe void Main(string[] args)
        {
            string ttf = args[0];
            string outName = args[1];
            int size = int.Parse(args[2]);

            FTLibrary library = new FTLibrary();
            FreeType.InitFreeType(ref library);

            Span<byte> pathName = Encoding.UTF8.GetBytes(ttf).AsSpan();

            FTFace face = new FTFace();
            fixed (byte* ptr = pathName)
                FreeType.NewFace(library, ptr, 0, ref face);

            FreeType.SetPixelSizes(face, 0, (uint)size);

            using BinaryWriter br = new BinaryWriter(File.OpenWrite(outName + ".bin"), Encoding.UTF8, false);

            for (byte c = 0; c < 128; c++)
            {

            }
        }
    }
}
