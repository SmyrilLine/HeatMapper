using CsvHelper;
using CsvHelper.Configuration;
using OpenCvSharp;
using System.Globalization;

if (args.Length != 2)
    throw new ArgumentException("I need a dimention and a file, eg .. Heatmapper.exe 400x400 myfile.csv");

var d = args[0].Split('x').Select(x => int.Parse(x)).ToArray();
IEnumerable<HeatPoint> heatPoints;

var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    Delimiter = ";",
};

using (var reader = new StreamReader(args[1]))
using (var csv = new CsvReader(reader, csvConfig))
{
    heatPoints = csv.GetRecords<HeatPoint>().ToList();
}

var dst = new Mat(new Size(d[0], d[1]), MatType.CV_8UC1, new Scalar(0));

foreach (var hp in heatPoints)
{
    AddPoint(dst, new Point(hp.X, hp.Y), hp.R, hp.S);
}
dst.SaveImage("heatmap_points.jpg");
ApplyFade(dst);
dst.SaveImage("heatmap_falloff.jpg");
ToHeatMap(dst);
dst.SaveImage("heatmap_final.jpg");

void AddPoint(Mat m, Point p, int r, float strength)
{
    int s = (int)Math.Round(255 * strength);
    Mat m2 = new(m.Size(), MatType.CV_8UC1, new Scalar(0));
    Cv2.Circle(m2, p, r, new Scalar(s, s, s), -1);
    Cv2.Add(m, m2, m);
}

void ApplyFade(Mat m)
{
    Cv2.DistanceTransform(m, m, DistanceTypes.L2, DistanceTransformMasks.Precise, 3);
    Cv2.Normalize(m, m, 0, 1, NormTypes.MinMax);
    m.ConvertTo(m, MatType.CV_8UC1, 255, 0);
}

void ToHeatMap(Mat m)
{
    Cv2.ApplyColorMap(m, m, ColormapTypes.Jet);
}

struct HeatPoint
{
    public int X { get; set; }
    public int Y { get; set; }
    public int R { get; set; }
    public float S { get; set; }
}
