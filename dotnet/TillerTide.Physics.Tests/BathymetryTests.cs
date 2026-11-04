using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using TillerTide.Physics;
using TillerTide.Physics.Region;
using Xunit;

public class BathymetryTests
{
    /// <summary>Writes a little single-band float32 GeoTIFF, as EMODnet's WCS returns.</summary>
    private static byte[] GeoTiff(int w, int h, Func<int, int, float> value, double west, double north, double res,
        bool tiled = false, bool deflate = false, string noData = null)
    {
        var pixels = new List<byte[]>();
        int tw = 16, th = 16;
        if (tiled)
        {
            for (int ty = 0; ty < h; ty += th)
                for (int tx = 0; tx < w; tx += tw)
                {
                    var b = new byte[tw * th * 4];
                    for (int y = 0; y < th; y++)
                        for (int x = 0; x < tw; x++)
                            if (tx + x < w && ty + y < h) BitConverter.GetBytes(value(tx + x, ty + y)).CopyTo(b, (y * tw + x) * 4);
                    pixels.Add(b);
                }
        }
        else
        {
            var b = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) BitConverter.GetBytes(value(x, y)).CopyTo(b, (y * w + x) * 4);
            pixels.Add(b);
        }
        if (deflate)
            for (int i = 0; i < pixels.Count; i++)
            {
                using var ms = new MemoryStream();
                ms.WriteByte(0x78); ms.WriteByte(0x9C);
                using (var z = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true)) z.Write(pixels[i], 0, pixels[i].Length);
                pixels[i] = ms.ToArray();
            }

        var outp = new MemoryStream();
        var bw = new BinaryWriter(outp);
        bw.Write((byte)'I'); bw.Write((byte)'I'); bw.Write((ushort)42); bw.Write(0u);
        var offsets = new List<uint>();
        foreach (var p in pixels) { offsets.Add((uint)outp.Position); bw.Write(p); }
        uint offArr = (uint)outp.Position; foreach (var o in offsets) bw.Write(o);
        uint cntArr = (uint)outp.Position; foreach (var p in pixels) bw.Write((uint)p.Length);
        uint scale = (uint)outp.Position; bw.Write(res); bw.Write(res); bw.Write(0.0);
        uint tie = (uint)outp.Position; bw.Write(0.0); bw.Write(0.0); bw.Write(0.0); bw.Write(west); bw.Write(north); bw.Write(0.0);
        uint nd = (uint)outp.Position; byte[] ndBytes = noData == null ? null : Encoding.ASCII.GetBytes(noData + "\0");
        if (ndBytes != null) bw.Write(ndBytes);
        if (outp.Position % 2 == 1) bw.Write((byte)0);

        var entries = new List<(ushort tag, ushort type, uint count, uint value)>
        {
            (256, 4, 1, (uint)w), (257, 4, 1, (uint)h), (258, 3, 1, 32), (259, 3, 1, deflate ? 8u : 1u), (277, 3, 1, 1),
        };
        if (tiled) { entries.Add((322, 3, 1, (uint)tw)); entries.Add((323, 3, 1, (uint)th)); }
        else entries.Add((278, 4, 1, (uint)h));
        uint n = (uint)pixels.Count;
        entries.Add(((ushort)(tiled ? 324 : 273), 4, n, n == 1 ? offsets[0] : offArr));
        entries.Add(((ushort)(tiled ? 325 : 279), 4, n, n == 1 ? (uint)pixels[0].Length : cntArr));
        entries.Add((339, 3, 1, 3));
        entries.Add((33550, 12, 3, scale));
        entries.Add((33922, 12, 6, tie));
        if (ndBytes != null) entries.Add((42113, 2, (uint)ndBytes.Length, nd));
        entries.Sort((a, b) => a.tag.CompareTo(b.tag));

        uint ifd = (uint)outp.Position;
        bw.Write((ushort)entries.Count);
        foreach (var e in entries) { bw.Write(e.tag); bw.Write(e.type); bw.Write(e.count); bw.Write(e.value); }
        bw.Write(0u);
        bw.Flush();
        var bytes = outp.ToArray();
        BitConverter.GetBytes(ifd).CopyTo(bytes, 4);
        return bytes;
    }

    private static readonly GeoBounds Bounds = new GeoBounds(56.0, 12.5, 56.1, 12.7);

    [Fact]
    public void Reads_float_geotiff_with_georeference()
    {
        // 40 x 20 cells of 0.005°, elevation getting deeper to the east: -x metres.
        var tif = GeoTiff(40, 20, (x, y) => -(x + 0.5f), 12.5, 56.1, 0.005);
        var b = Bathymetry.FromGeoTiff(tif, Bounds);
        Assert.Equal(40, b.Width);
        Assert.Equal(12.7, b.East, 6);
        Assert.Equal(56.0, b.South, 6);
        Assert.Equal(10.5, b.DepthAt(56.05, 12.5 + 10.5 * 0.005).Value, 3); // cell 10 centre, elevation -10.5 -> depth 10.5
        Assert.Null(b.DepthAt(57, 12.6));
    }

    [Fact]
    public void Reads_tiled_deflate_tiff_and_skips_nodata()
    {
        var tif = GeoTiff(40, 20, (x, y) => x < 5 ? -99999f : -20f, 12.5, 56.1, 0.005, tiled: true, deflate: true, noData: "-99999");
        var b = Bathymetry.FromGeoTiff(tif, Bounds);
        Assert.Equal(20, b.DepthAt(56.05, 12.65).Value, 3);
        Assert.Null(b.DepthAt(56.05, 12.505));
        // Bilinear next to a no-data cell only uses the cells that have data.
        Assert.Equal(20, b.DepthAt(56.05, 12.5 + 5.2 * 0.005).Value, 3);
    }

    [Fact]
    public void Region_depth_uses_soundings_offshore_and_the_shelf_near_the_beach()
    {
        var proj = new GeoProjection(56.05, 12.6);
        // Coastline running north along 12.6 E: land to the west.
        var c = new Coastline();
        c.Lines.Add(new[] { proj.ToLocal(55.9, 12.6), proj.ToLocal(56.2, 12.6) });
        var min = proj.ToLocal(56.0, 12.5); var max = proj.ToLocal(56.1, 12.7);
        var grid = RegionGrid.Build(c, min, max, cellSize: 50, searchRadius: 1500);
        double estimated = grid.DepthAt(proj.ToLocal(56.05, 12.68));

        var b = Bathymetry.FromGeoTiff(GeoTiff(40, 20, (x, y) => x == 32 ? -2.0f : -35f, 12.5, 56.1, 0.005), Bounds);
        grid.SetBathymetry(b, proj);
        Assert.Equal(35, grid.DepthAt(proj.ToLocal(56.05, 12.68)), 1);         // deep water ~5 km out
        Assert.NotEqual(estimated, grid.DepthAt(proj.ToLocal(56.05, 12.68)));
        double shoal = grid.DepthAt(proj.ToLocal(56.05, 12.5 + 32.5 * 0.005));
        Assert.True(shoal < 3, $"shoal {shoal}, sd {grid.SignedDistanceAt(proj.ToLocal(56.05, 12.5 + 32.5 * 0.005))}");  // a real shoal
        Assert.InRange(grid.DepthAt(proj.ToLocal(56.05, 12.6008)), 2, 8);       // 50 m off the beach: still shelving
        Assert.True(grid.IsLand(proj.ToLocal(56.05, 12.59)));
        Assert.Contains("emodnet:mean", Bathymetry.EmodnetUrl(Regions.Helsingor.Bounds));
    }
}
