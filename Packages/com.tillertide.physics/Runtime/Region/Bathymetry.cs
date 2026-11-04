using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace TillerTide.Physics.Region
{
    /// <summary>
    /// Measured water depths on a lat/lon grid, e.g. the EMODnet digital bathymetry (about 115 m cells around
    /// Denmark). Row 0 is the northern edge. Depth is positive below the surface; NaN where there is no data.
    /// </summary>
    public sealed class Bathymetry
    {
        public double West { get; }
        public double South { get; }
        public double East { get; }
        public double North { get; }
        public int Width { get; }
        public int Height { get; }
        public float[] Depth { get; }

        public Bathymetry(double west, double south, double east, double north, int width, int height, float[] depth)
        {
            if (depth.Length != width * height) throw new ArgumentException("Depth array does not match the size.");
            West = west; South = south; East = east; North = north; Width = width; Height = height; Depth = depth;
        }

        /// <summary>EMODnet WCS request for a GeoTIFF of mean depth over the bounds, at the native 1/16 arc-minute.</summary>
        public static string EmodnetUrl(GeoBounds b)
        {
            const double res = 1.0 / 960.0 * 2; // 1/16 arc-minute is 1/960°; ask for half resolution (~230 m) to keep it small
            return string.Format(CultureInfo.InvariantCulture,
                "https://ows.emodnet-bathymetry.eu/wcs?service=WCS&version=1.0.0&request=GetCoverage&coverage=emodnet:mean" +
                "&crs=EPSG:4326&bbox={0},{1},{2},{3}&format=image/tiff&interpolation=nearest&resx={4}&resy={4}",
                b.West, b.South, b.East, b.North, res);
        }

        /// <summary>Depth (m) at a position, bilinear; null outside the grid or where there is no data.</summary>
        public double? DepthAt(double lat, double lon)
        {
            double fx = (lon - West) / (East - West) * Width - 0.5;
            double fy = (North - lat) / (North - South) * Height - 0.5;
            if (fx < -0.5 || fy < -0.5 || fx > Width - 0.5 || fy > Height - 0.5) return null;
            int x0 = Math.Max(0, Math.Min(Width - 1, (int)Math.Floor(fx))), y0 = Math.Max(0, Math.Min(Height - 1, (int)Math.Floor(fy)));
            int x1 = Math.Min(x0 + 1, Width - 1), y1 = Math.Min(y0 + 1, Height - 1);
            double tx = Math.Max(0, Math.Min(1, fx - x0)), ty = Math.Max(0, Math.Min(1, fy - y0));
            double sum = 0, wsum = 0;
            void Add(int x, int y, double w)
            {
                float v = Depth[y * Width + x];
                if (float.IsNaN(v) || w <= 0) return;
                sum += v * w; wsum += w;
            }
            Add(x0, y0, (1 - tx) * (1 - ty)); Add(x1, y0, tx * (1 - ty)); Add(x0, y1, (1 - tx) * ty); Add(x1, y1, tx * ty);
            return wsum > 1e-6 ? sum / wsum : (double?)null;
        }

        /// <summary>
        /// Reads a single-band GeoTIFF (uncompressed or Deflate, strips or tiles, float/int samples). Values are taken
        /// as elevation (negative below sea level) unless most of them are positive, in which case they are depths.
        /// The georeferencing tags give the bounds; without them the requested bounds are assumed.
        /// </summary>
        public static Bathymetry FromGeoTiff(byte[] data, GeoBounds requested)
        {
            var t = new TiffReader(data);
            float[] values = t.ReadBand();
            int w = t.Width, h = t.Height;
            double west = requested.West, north = requested.North, east = requested.East, south = requested.South;
            if (t.PixelScale != null && t.Tiepoint != null && t.PixelScale.Length >= 2 && t.Tiepoint.Length >= 6)
            {
                double sx = t.PixelScale[0], sy = t.PixelScale[1];
                west = t.Tiepoint[3] - t.Tiepoint[0] * sx;
                north = t.Tiepoint[4] + t.Tiepoint[1] * sy;
                east = west + w * sx;
                south = north - h * sy;
            }

            int positive = 0, valid = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float v = values[i];
                if (t.NoData.HasValue && Math.Abs(v - t.NoData.Value) < 1e-3) v = values[i] = float.NaN;
                if (float.IsNaN(v) || v < -12000 || v > 12000) { values[i] = float.NaN; continue; }
                valid++;
                if (v > 0) positive++;
            }
            bool isElevation = positive <= valid / 2;
            for (int i = 0; i < values.Length; i++)
                if (!float.IsNaN(values[i])) values[i] = isElevation ? -values[i] : values[i];
            return new Bathymetry(west, south, east, north, w, h, values);
        }

        private sealed class TiffReader
        {
            private readonly byte[] _d;
            private readonly bool _le;
            public int Width, Height, Bits = 32, Format = 1, Compression = 1, Predictor = 1, SamplesPerPixel = 1;
            public long[] StripOffsets, StripCounts, TileOffsets, TileCounts;
            public int RowsPerStrip, TileW, TileH;
            public double[] PixelScale, Tiepoint;
            public double? NoData;

            public TiffReader(byte[] d)
            {
                _d = d;
                if (d.Length < 8) throw new InvalidDataException("Not a TIFF");
                if (d[0] == 'I' && d[1] == 'I') _le = true;
                else if (d[0] == 'M' && d[1] == 'M') _le = false;
                else throw new InvalidDataException("Not a TIFF");
                if (U16(2) != 42) throw new InvalidDataException("Not a classic TIFF");
                long ifd = U32(4);
                int n = U16(ifd);
                for (int i = 0; i < n; i++)
                {
                    long e = ifd + 2 + i * 12;
                    int tag = U16(e), type = U16(e + 2);
                    long count = U32(e + 4);
                    var vals = Values(e, type, count);
                    switch (tag)
                    {
                        case 256: Width = (int)vals[0]; break;
                        case 257: Height = (int)vals[0]; break;
                        case 258: Bits = (int)vals[0]; break;
                        case 259: Compression = (int)vals[0]; break;
                        case 277: SamplesPerPixel = (int)vals[0]; break;
                        case 273: StripOffsets = ToLong(vals); break;
                        case 278: RowsPerStrip = (int)vals[0]; break;
                        case 279: StripCounts = ToLong(vals); break;
                        case 317: Predictor = (int)vals[0]; break;
                        case 322: TileW = (int)vals[0]; break;
                        case 323: TileH = (int)vals[0]; break;
                        case 324: TileOffsets = ToLong(vals); break;
                        case 325: TileCounts = ToLong(vals); break;
                        case 339: Format = (int)vals[0]; break;
                        case 33550: PixelScale = vals; break;
                        case 33922: Tiepoint = vals; break;
                        case 42113:
                            if (double.TryParse(Ascii(e, count).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var nd)) NoData = nd;
                            break;
                    }
                }
                if (Width <= 0 || Height <= 0) throw new InvalidDataException("TIFF has no size");
                if (Compression != 1 && Compression != 8 && Compression != 32946) throw new NotSupportedException($"TIFF compression {Compression}");
                if (Predictor != 1) throw new NotSupportedException($"TIFF predictor {Predictor}");
                if (RowsPerStrip <= 0) RowsPerStrip = Height;
            }

            public float[] ReadBand()
            {
                var o = new float[Width * Height];
                int bps = Bits / 8 * SamplesPerPixel;
                if (TileOffsets != null)
                {
                    int across = (Width + TileW - 1) / TileW;
                    for (int k = 0; k < TileOffsets.Length; k++)
                    {
                        var block = Block(TileOffsets[k], TileCounts[k]);
                        int tx = k % across * TileW, ty = k / across * TileH;
                        for (int y = 0; y < TileH; y++)
                            for (int x = 0; x < TileW; x++)
                            {
                                int gx = tx + x, gy = ty + y;
                                if (gx >= Width || gy >= Height) continue;
                                o[gy * Width + gx] = Sample(block, (y * TileW + x) * bps);
                            }
                    }
                }
                else
                {
                    for (int k = 0; k < StripOffsets.Length; k++)
                    {
                        var block = Block(StripOffsets[k], StripCounts != null ? StripCounts[k] : (long)RowsPerStrip * Width * bps);
                        int y0 = k * RowsPerStrip;
                        for (int y = 0; y < RowsPerStrip && y0 + y < Height; y++)
                            for (int x = 0; x < Width; x++)
                            {
                                int off = (y * Width + x) * bps;
                                if (off + Bits / 8 > block.Length) break;
                                o[(y0 + y) * Width + x] = Sample(block, off);
                            }
                    }
                }
                return o;
            }

            private byte[] Block(long offset, long count)
            {
                var raw = new byte[count];
                Array.Copy(_d, offset, raw, 0, count);
                if (Compression == 1) return raw;
                // Deflate in TIFF is a zlib stream: skip the 2-byte header.
                using (var ms = new MemoryStream(raw, 2, raw.Length - 2))
                using (var z = new DeflateStream(ms, CompressionMode.Decompress))
                using (var outp = new MemoryStream())
                {
                    z.CopyTo(outp);
                    return outp.ToArray();
                }
            }

            private float Sample(byte[] b, int i)
            {
                switch (Format)
                {
                    case 3 when Bits == 32: return BitConverter.ToSingle(Ordered(b, i, 4), 0);
                    case 3 when Bits == 64: return (float)BitConverter.ToDouble(Ordered(b, i, 8), 0);
                    case 2 when Bits == 16: return BitConverter.ToInt16(Ordered(b, i, 2), 0);
                    case 2 when Bits == 32: return BitConverter.ToInt32(Ordered(b, i, 4), 0);
                    case 1 when Bits == 16: return BitConverter.ToUInt16(Ordered(b, i, 2), 0);
                    case 1 when Bits == 8: return b[i];
                    default: throw new NotSupportedException($"TIFF sample format {Format}/{Bits}");
                }
            }

            private byte[] Ordered(byte[] b, int i, int n)
            {
                var r = new byte[n];
                Array.Copy(b, i, r, 0, n);
                if (_le != BitConverter.IsLittleEndian) Array.Reverse(r);
                return r;
            }

            private int U16(long i) => _le ? _d[i] | _d[i + 1] << 8 : _d[i] << 8 | _d[i + 1];
            private long U32(long i) => _le
                ? (uint)(_d[i] | _d[i + 1] << 8 | _d[i + 2] << 16 | _d[i + 3] << 24)
                : (uint)(_d[i] << 24 | _d[i + 1] << 16 | _d[i + 2] << 8 | _d[i + 3]);

            private static int Size(int type) => type == 3 || type == 8 ? 2 : type == 4 || type == 9 || type == 11 ? 4 : type == 12 || type == 5 ? 8 : 1;

            private double[] Values(long entry, int type, long count)
            {
                long total = Size(type) * count;
                long at = total <= 4 ? entry + 8 : U32(entry + 8);
                var v = new double[Math.Min(count, 1 << 20)];
                for (int i = 0; i < v.Length; i++)
                {
                    long p = at + i * Size(type);
                    switch (type)
                    {
                        case 3: v[i] = U16(p); break;
                        case 4: v[i] = U32(p); break;
                        case 12: v[i] = BitConverter.ToDouble(Ordered(_d, (int)p, 8), 0); break;
                        case 11: v[i] = BitConverter.ToSingle(Ordered(_d, (int)p, 4), 0); break;
                        default: v[i] = _d[p]; break;
                    }
                }
                return v;
            }

            private string Ascii(long entry, long count)
            {
                long at = count <= 4 ? entry + 8 : U32(entry + 8);
                var chars = new List<char>();
                for (long i = 0; i < count && _d[at + i] != 0; i++) chars.Add((char)_d[at + i]);
                return new string(chars.ToArray());
            }

            private static long[] ToLong(double[] v)
            {
                var r = new long[v.Length];
                for (int i = 0; i < v.Length; i++) r[i] = (long)v[i];
                return r;
            }
        }
    }
}
