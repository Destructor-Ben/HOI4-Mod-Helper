using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace HOI4ModHelper.Dds;

public static class DdsImage
{
    public static void EncodeAsDds(this Image<Rgba32> image, string path)
    {
        using var fs = new FileStream(path, FileMode.OpenOrCreate);
        image.EncodeAsDds(fs);
    }

    private static void EncodeAsDds(this Image<Rgba32> image, Stream stream)
    {
        var writer = new BinaryWriter(stream);

        // Write the magic
        writer.Write('D');
        writer.Write('D');
        writer.Write('S');
        writer.Write(' ');

        // Write the header
        writer.Write(124); // Header size
        writer.Write(0); // Flags
        writer.Write(image.Height);
        writer.Write(image.Width);
        writer.Write(0); // Pitch/linear size
        writer.Write(0); // Depth
        writer.Write(0); // Mipmap count

        // dwReserved1
        for (int i = 0; i < 11; i++)
        {
            writer.Write(0);
        }

        writer.Write(0); // Pixel format
        writer.Write(0); // Caps
        writer.Write(0); // Caps 2

        // dwCaps3, dwCaps4, dwReserved2
        for (int i = 0; i < 3; i++)
        {
            writer.Write(0);
        }

        // Write the data
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var color = image[x, y];

                if (color.A == 0)
                    color = new Rgba32(0, 0, 0, 0);

                writer.Write(color.A);
                writer.Write(color.R);
                writer.Write(color.G);
                writer.Write(color.B);
            }
        }
    }
}
