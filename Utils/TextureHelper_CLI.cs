using System;
using System.IO;
using System.Linq;
using IGAE_GUI.IGZ;
using BCnEncoder.Decoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace IGAE_GUI.Utils
{
    /// <summary>
    /// Minimal TextureHelper for CLI - without Windows.Forms dependency
    /// Provides only PNG export functionality
    /// </summary>
    public static class TextureHelper
    {
        public static bool Extract(Stream input, Stream output, ushort width, ushort height, uint size, ushort mipmaps, IGZ_TextureFormat format, bool leaveOpen)
        {
            try
            {
                byte[] textureData = new byte[size];
                input.Read(textureData, 0, (int)size);

                // Decode using BCnEncoder
                var decoder = new BcDecoder();
                var imageData = decoder.DecodeRaw(textureData, width, height, format.ToDXGIFormat());

                // Convert to Image<Rgba32>
                var image = Image.LoadPixelData<Rgba32>(imageData, width, height);

                // Save as PNG to output stream
                image.SaveAsPng(output);
                
                if (!leaveOpen)
                    output.Close();

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error extracting texture: {ex.Message}");
                return false;
            }
        }

        public static void Replace(Stream input, Stream output, ushort width, ushort height, uint size, ushort mipmaps, IGZ_TextureFormat format, bool mode)
        {
            throw new NotImplementedException("Texture replacement not yet implemented in CLI");
        }

        public static void ExtractDDS(Stream input, Stream output, ushort width, ushort height, uint size, ushort mipmaps, IGZ_TextureFormat format, bool leaveOpen)
        {
            try
            {
                byte[] textureData = new byte[size];
                input.Read(textureData, 0, (int)size);

                // Write DDS header for DX10 format
                WriteDDSHeader(output, width, height, format);
                
                // Write raw texture data
                output.Write(textureData, 0, textureData.Length);

                if (!leaveOpen)
                    output.Close();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error extracting DDS: {ex.Message}");
            }
        }

        public static void ReplaceDDS(Stream input, Stream output, ushort width, uint size, IGZ_TextureFormat format)
        {
            throw new NotImplementedException("DDS replacement not yet implemented in CLI");
        }

        private static void WriteDDSHeader(Stream output, ushort width, ushort height, IGZ_TextureFormat format)
        {
            using (BinaryWriter bw = new BinaryWriter(output, System.Text.Encoding.ASCII, true))
            {
                bw.Write(0x20534444u); // "DDS "

                // DDS Header
                bw.Write(124); // size
                bw.Write(0x0010100Fu); // flags
                bw.Write((uint)height); // height
                bw.Write((uint)width); // width
                bw.Write((width * height * 4)); // pitch
                bw.Write(1u); // depth
                bw.Write(1u); // mipmaps
                
                // Reserved fields
                for (int i = 0; i < 11; i++)
                    bw.Write(0u);

                // Pixel format
                bw.Write(32u); // size
                bw.Write(0x41u); // flags (FOURCC)
                bw.Write(0x30315844u); // "DX10"
                bw.Write(0u); // bits
                bw.Write(0u); // masks
                bw.Write(0u);
                bw.Write(0u);
                bw.Write(0u);

                // Caps and reserved
                bw.Write(0x1000u);
                bw.Write(0u);
                bw.Write(0u);
                bw.Write(0u);

                // DX10 Extended Header (required for most formats)
                bw.Write(ConvertFormatToDX10((uint)format.ToDXGIFormat()));
                bw.Write(3u); // D3D10_RESOURCE_DIMENSION_TEXTURE2D
                bw.Write(0u); // flags
                bw.Write(1u); // array size
                bw.Write(0u); // reserved
            }
        }

        private static uint ConvertFormatToDX10(uint dxgiFormat)
        {
            return dxgiFormat;
        }
    }

    /// <summary>
    /// Extension method to convert IGZ_TextureFormat to DXGI format
    /// </summary>
    public static class TextureFormatExtensions
    {
        public static uint ToDXGIFormat(this IGZ_TextureFormat format)
        {
            return (uint)format switch
            {
                // BC Compressed formats
                (uint)IGZ_TextureFormat.BC1 => 71u,  // DXGI_FORMAT_BC1_UNORM
                (uint)IGZ_TextureFormat.BC3 => 77u,  // DXGI_FORMAT_BC3_UNORM
                (uint)IGZ_TextureFormat.BC4 => 80u,  // DXGI_FORMAT_BC4_UNORM
                (uint)IGZ_TextureFormat.BC5 => 83u,  // DXGI_FORMAT_BC5_UNORM
                (uint)IGZ_TextureFormat.BC6 => 95u,  // DXGI_FORMAT_BC6H_UF16
                (uint)IGZ_TextureFormat.BC7 => 98u,  // DXGI_FORMAT_BC7_UNORM
                
                // Standard formats
                (uint)IGZ_TextureFormat.R8G8B8A8 => 28u,  // DXGI_FORMAT_R8G8B8A8_UNORM
                (uint)IGZ_TextureFormat.R8G8B8 => 24u,   // DXGI_FORMAT_B8G8R8_UNORM
                (uint)IGZ_TextureFormat.R4G4B4A4 => 12u,  // DXGI_FORMAT_B4G4R4A4_UNORM
                
                _ => 28u // Default to RGBA
            };
        }
    }
}
