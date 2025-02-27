using Schedulers;
using SimpleEditor.Bindings;
using System.Text;
using Tomlyn;
using Tomlyn.Model;

namespace SimpleEditor.Import.Processors
{
    public class ImageImporter : IJob
    {
        public static Queue<Arguments> Pending = new Queue<Arguments>();
        public static NvttQuality CompressionQuality = NvttQuality.Normal;

        public unsafe void Execute()
        {
            Arguments args;
            lock (Pending)
            {
                if (Pending.Count == 0)
                {
                    LogTypes.Import.Warning("Image import scheduled but no arguments are available!");
                    return;
                }

                args = Pending.Dequeue();
            }

            TomlTable table = Toml.ToModel(args.EdRuntime.ProjectFileSystem.ReadAssociate(args.Id));

            TomlTable general = (TomlTable)table["General"];
            TomlTable mipmaps = (TomlTable)table["Mipmaps"];
            TomlTable image = (TomlTable)table["Image"];

            NvttBoolean hasAlpha = NvttBoolean.True;
            byte alphaBits = 8;

            Span<byte> imageBytes = args.EdRuntime.ProjectFileSystem.ReadRealBytes(args.Id);

            NvttSurface* surface = NVTT.nvttCreateSurface();
            fixed (byte* ptr = imageBytes)
            {
                if (NVTT.nvttSurfaceLoadFromMemory(surface, ptr, (ulong)imageBytes.Length, &hasAlpha, NvttBoolean.False, null) != NvttBoolean.True)
                {
                    LogTypes.Import.Error("Failed to load surface for import: \"{}\"", args.Id);
                    NVTT.nvttDestroySurface(surface);
                    return;
                }
            }

            if ((bool)image["FlipVertical"])
                NVTT.nvttSurfaceFlipY(surface, null);

            NvttCompressionOptions* compressionOptions = NVTT.nvttCreateCompressionOptions();
            NVTT.nvttSetCompressionOptionsQuality(compressionOptions, CompressionQuality);

            NvttOutputOptions* outputOptions = NVTT.nvttCreateOutputOptions();

            NvttErrorHandlerDelegate errorHandler = NvttErrorHandler;
            NVTT.nvttSetOutputOptionsErrorHandler(outputOptions, NvttErrorHandler);

            ImageFormat im = (ImageFormat)(long)general["Format"];
            switch (im)
            {
                case ImageFormat.BC7: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC7); break;
                case ImageFormat.BC6S: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC6S); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); break;
                case ImageFormat.BC6U: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC6U); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedFloat); break;
                case ImageFormat.ASTC: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.ASTC_LDR_4x4); break;
                case ImageFormat.BC5u: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC5); break;
                case ImageFormat.BC4u: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC4); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); break;
                case ImageFormat.BC3: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC3); break;
                case ImageFormat.BC3n: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC3n); break;
                //case ImageFormat.BC3n_agbr: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.(BC3_RGBM); break;
                case ImageFormat.BC2:
                    NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC2);
                    if ((bool)image["CutoutDither"])
                    {
                        byte bits = 4;
                        if (alphaBits != bits)
                            bits = alphaBits;
                        NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, bits);
                        NVTT.nvttSetCompressionOptionsQuantization(compressionOptions, NvttBoolean.False, NvttBoolean.True, NvttBoolean.False, 0);
                    }
                    break;
                case ImageFormat.BC1a: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC1a); NVTT.nvttSetCompressionOptionsQuantization(compressionOptions, NvttBoolean.False, (bool)image["CutoutDither"] ? NvttBoolean.True : NvttBoolean.False, NvttBoolean.False, 0); break;
                case ImageFormat.BC1: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.BC1); break;
                case ImageFormat._8a: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 0, 0, 0); break;
                case ImageFormat._8l: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 8, 0xff, 0, 0, 0); break;
                case ImageFormat._888_bgr: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, 0); break;
                case ImageFormat._8888_bgra: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, 8); break;
                case ImageFormat._888x_bgrx: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, 8); break;
                case ImageFormat._888_rgb: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, 0); break;
                case ImageFormat._8888_rgba: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.UnsignedNorm); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, 8); break;
                case ImageFormat._16f: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 16, 0, 0, 0); break;
                case ImageFormat._16f16f: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 16, 16, 0, 0); break;
                case ImageFormat._16x4f: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 16, 16, 16, 16); break;
                case ImageFormat._32f: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 32, 0, 0, 0); break;
                case ImageFormat._32f32f: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 32, 32, 0, 0); break;
                case ImageFormat._32x4f: NVTT.nvttSetCompressionOptionsFormat(compressionOptions, NvttFormat.RGBA); NVTT.nvttSetCompressionOptionsPixelType(compressionOptions, NvttPixelType.Float); NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 32, 32, 32, 32); break;
                default: break;
            }

            if (im <= ImageFormat.BC6U)
                NVTT.nvttSetOutputOptionsContainer(outputOptions, NvttContainer.DDS10);
            else
                NVTT.nvttSetOutputOptionsContainer(outputOptions, NvttContainer.DDS);

            if ((bool)image["CutoutDither"] && hasAlpha == NvttBoolean.True && im != ImageFormat.BC2 && im != ImageFormat.BC1a)
            {
                byte bits = 8;
                if (alphaBits != bits)
                    bits = alphaBits;
                NVTT.nvttSetCompressionOptionsPixelFormat(compressionOptions, 0, 8, 8, 8, bits);
                NVTT.nvttSetCompressionOptionsQuantization(compressionOptions, NvttBoolean.False, NvttBoolean.True, NvttBoolean.False, (int)MathF.Round((float)(double)image["CutoutThreshold"] * 255));
            }

            if ((bool)mipmaps["GammaCorrect"])
            {
                NVTT.nvttSurfaceToSrgbUnclamped(surface, null);
            }

            NvttAlphaMode alphaMode = NVTT.nvttSurfaceAlphaMode(surface);

            bool alphaPremultiplied = (bool)image["PremultipliedAlpha"];
            alphaMode = alphaPremultiplied ? NvttAlphaMode.Premultiplied : alphaMode;
            if (im == ImageFormat.BC6U || im == ImageFormat.BC6S)
                alphaMode = NvttAlphaMode.None;
            NVTT.nvttSetSurfaceAlphaMode(surface, alphaMode);

            NvttMipmapFilter filter = NvttMipmapFilter.Box;
            switch ((ImageFilterType)(long)mipmaps["FilterType"])
            {
                default:
                case ImageFilterType.Box: filter = NvttMipmapFilter.Box; break;
                case ImageFilterType.Kaiser: filter = NvttMipmapFilter.Kaiser; break;
                case ImageFilterType.Triangle: filter = NvttMipmapFilter.Triangle; break;
                case ImageFilterType.Mitchell_Netravali: filter = NvttMipmapFilter.Mitchell; break;
                case ImageFilterType.Min: filter = NvttMipmapFilter.Min; break;
                case ImageFilterType.Max: filter = NvttMipmapFilter.Max; break;
            }

            int maxMips = (int)(long)mipmaps["MaxMipmapCount"];
            int minMipSize = (int)(long)mipmaps["MinMipmapSize"];

            int mip0Width = NVTT.nvttSurfaceWidth(surface);
            int mip0Height = NVTT.nvttSurfaceHeight(surface);
            int mip0Depth = NVTT.nvttSurfaceDepth(surface);

            List<Tuple<int, int>> sizes = new List<Tuple<int, int>>();

            int mipmapCount = 0;
            if ((bool)general["GenerateMipmaps"])
            {
                while (mipmapCount < maxMips)
                {
                    int mipWidth = Math.Max(1, mip0Width >> mipmapCount);
                    int mipHeight = Math.Max(1, mip0Height >> mipmapCount);

                    if ((mipWidth < minMipSize) || (mipHeight < minMipSize))
                    {
                        break;
                    }

                    sizes.Add(new Tuple<int, int>(mipWidth, mipHeight));
                    mipmapCount++;
                }
            }

            mipmapCount = Math.Max(1, mipmapCount);

            OutputHandler outputHandler = new OutputHandler(args.Output);
            NVTT.nvttSetOutputOptionsOutputHandler(outputOptions,
                outputHandler.nvttBeginImage,
                outputHandler.nvttWriteData,
                outputHandler.nvttEndImage);

            switch ((ImageType)(long)general["Type"])
            {
                case ImageType.ColorMap: break;
                case ImageType.Grayscale: break;
                case ImageType.NormalMap_TangentSpace: NVTT.nvttSetSurfaceNormalMap(surface, NvttBoolean.True); break;
                case ImageType.NormalMap_ObjectSpace: NVTT.nvttSetSurfaceNormalMap(surface, NvttBoolean.True); break;
                default: break;
            }

            NvttBatchList* batchList = NVTT.nvttCreateBatchList();

            if (NVTT.nvttSurfaceIsNormalMap(surface) == NvttBoolean.True)
            {
                if ((ImageType)(long)general["Type"] == ImageType.NormalMap_TangentSpace)
                {
                    NVTT.nvttSurfaceToGreyScale(surface, 1.0f / 3.0f, 1.0f / 3.0f, 1.0f / 3.0f, 0.0f, null);
                    NVTT.nvttSurfaceToNormalMap(surface, 1.0f / 1.875f, 0.5f / 1.875f, 0.25f / 1.875f, 0.125f / 1.875f, null);
                }
            }
            else
            {
                if (mipmapCount > 1 && (bool)mipmaps["GammaCorrect"])
                {
                    NVTT.nvttSurfaceToLinearFromSrgbUnclamped(surface, null);
                }
            }

            NvttSurface* tmp = NVTT.nvttSurfaceClone(surface);
            if (NVTT.nvttSurfaceIsNormalMap(tmp) == NvttBoolean.False && mipmapCount > 1 && (bool)mipmaps["GammaCorrect"])
            {
                NVTT.nvttSurfaceToSrgbUnclamped(tmp, null);
            }

            List<nint> surfaces = new List<nint>();

            NvttContext* context = NVTT.nvttCreateContext();
            NVTT.nvttSetContextCudaAcceleration(context, NvttBoolean.False);
            NVTT.nvttContextQuantize(context, tmp, compressionOptions);
            NvttSurface* surf = tmp;
            NVTT.nvttBatchListAppend(batchList, surf, 0, 0, outputOptions);
            surfaces.Add((nint)surf);

            bool scaleMipAlpha = hasAlpha == NvttBoolean.True && mipmapCount > 0 && (bool)image["ScaleAlphaForMipmaps"];
            float threshold = (float)(double)image["CutoutThreshold"];
            float mip0_coverage = scaleMipAlpha ? NVTT.nvttSurfaceAlphaTestCoverage(surface, threshold, 3) : 0;

            for (int m = 0; m < mipmapCount; m++)
            {
                if (filter == NvttMipmapFilter.Kaiser)
                {
                    float[] @params = [1.0f, 4.0f];
                    fixed (float* ptr = @params)
                    {
                        NVTT.nvttSurfaceBuildNextMipmap(surface, NvttMipmapFilter.Kaiser, 3.0f, ptr, 1, null);
                    }
                }
                else
                {
                    NVTT.nvttSurfaceBuildNextMipmapDefaults(surface, filter, 1, null);
                }

                if (scaleMipAlpha)
                {
                    NVTT.nvttSurfaceScaleAlphaToCoverage(surface, mip0_coverage, threshold, 3, null);
                }

                if (NVTT.nvttSurfaceIsNormalMap(surface) == NvttBoolean.True)
                {
                    tmp = NVTT.nvttSurfaceClone(surface);
                }
                else
                {
                    tmp = NVTT.nvttSurfaceClone(surface);
                    if ((bool)mipmaps["GammaCorrect"])
                    {
                        NVTT.nvttSurfaceToSrgbUnclamped(tmp, null);
                    }
                }

                NVTT.nvttContextQuantize(context, tmp, compressionOptions);
                surf = tmp;
                NVTT.nvttBatchListAppend(batchList, surf, 0, m, outputOptions);
                surfaces.Add((nint)surf);
            }

            var cleanup = () =>
            {
                for (int i = 0; i < surfaces.Count; i++)
                {
                    NVTT.nvttDestroySurface((NvttSurface*)surfaces[i]);
                }

                NVTT.nvttDestroyBatchList(batchList);

                NVTT.nvttDestroySurface(surface);
                NVTT.nvttDestroyContext(context);

                NVTT.nvttDestroyOutputOptions(outputOptions);
                NVTT.nvttDestroyCompressionOptions(compressionOptions);

                outputHandler.Dispose();
            };

            if (NVTT.nvttContextOutputHeaderData(context, NvttTextureType._2D, mip0Width, mip0Height, mip0Depth, mipmapCount, NVTT.nvttSurfaceIsNormalMap(surface), compressionOptions, outputOptions) != NvttBoolean.True)
            {
                cleanup();
                LogTypes.Import.Error("Failed to set output header for import: \"{}\"", args.Id);
                return;
            }

            if (NVTT.nvttContextCompressBatch(context, batchList, compressionOptions) != NvttBoolean.True)
            {
                cleanup();
                LogTypes.Import.Error("Failed to compress output data for import: \"{}\"", args.Id);
                return;
            }

            cleanup();
        }

        public class Arguments
        {
            public Runtime.EditorRuntime EdRuntime;
            public ulong Id;
            public string Output;
        }

        private static void NvttErrorHandler(NvttError error)
        {
            string text = string.Empty;
            unsafe
            {
                byte* str = (byte*)NVTT.nvttErrorString(error);

                int length = 0;
                while (str[length++] != 0) ;

                text = Encoding.UTF8.GetString(str, length);
            }

            LogTypes.Import.Error("NVTT.nvttencountered an error: \"{}\" ()!", text, error);
        }
        private delegate void NvttErrorHandlerDelegate(NvttError error);

        public class OutputHandler : IDisposable
        {
            private Stream? _stream;

            public OutputHandler(string path)
            {
                try
                {
                    _stream = File.OpenWrite(path);
                }
                catch (Exception ex)
                {
                    LogTypes.Import.Error(ex, "Failed to open output stream for import!");
                }
            }

            public void Dispose()
            {
                _stream?.Dispose();
            }

            public void nvttBeginImage(int size, int width, int height, int depth, int face, int miplevel)
            {

            }

            public unsafe NvttBoolean nvttWriteData(void* data, int size)
            {
                if (_stream != null)
                {
                    _stream.Write(new ReadOnlySpan<byte>(data, size));
                    return NvttBoolean.True;
                }

                return NvttBoolean.False;
            }

            public void nvttEndImage()
            {

            }
        }
    }

    enum ImageFormat
    {
        BC7 = 0,
        BC6S,
        BC6U,
        ASTC,
        BC5u,
        BC4u,
        BC3,
        BC3n,
        BC2,
        BC1a,
        BC1,
        _8a,
        _8l,
        _888_bgr,
        _8888_bgra,
        _888x_bgrx,
        _888_rgb,
        _8888_rgba,
        _16f,
        _16f16f,
        _16x4f,
        _32f,
        _32f32f,
        _32x4f
    };

    enum ImageType
    {
        ColorMap,
        Grayscale,
        NormalMap_TangentSpace,
        NormalMap_ObjectSpace
    };

    enum ImageFilterType
    {
        Box,
        Kaiser,
        Triangle,
        Mitchell_Netravali,
        Min,
        Max
    };

    enum ImageColorSpace
    {
        Automatic,
        Linear,
        sRGB
    };
}
