using CsvHelper;
using CsvHelper.Configuration;
using OpenCvSharp;
using Svg;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.RegularExpressions;

if (args.Length != 2)
    throw new ArgumentException("I need a dimention and a file, eg .. Heatmapper.exe clean.svg myfile.csv");

IList<HeatPoint> heatPoints = new List<HeatPoint>();

var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    
    Delimiter = ",",
};

using (var reader = new StreamReader(args[1]))
using (var csv = new CsvReader(reader, csvConfig))
{
    csv.Read();
    csv.ReadHeader();
    while (csv.Read())
    {
        heatPoints.Add(new HeatPoint { Room = csv.GetField(0), dBm = csv.GetField<int>(1), Mbps = csv.GetField<int>(2) }); // = csv.GetRecords<HeatPoint>().ToList();
    }
}

var svgText = File.ReadAllText(args[0]);

// Clean the SVG
var newSvg = Regex.Replace(svgText, "fill:\\#[0-9a-fA-F]*\\;", "fill:none;");
var styleIndex = newSvg.IndexOf("<style>");
newSvg = newSvg.Insert(styleIndex + 7, ".textStyle{fill:#231f20 !important;}");
newSvg = Regex.Replace(newSvg, "\\<text class\\=\"[0-9a-z\\-]*\"", "<text class=\"textStyle\"");
string cleanName = $"{args[0]}.clean.svg";
File.WriteAllText(cleanName, newSvg);

// Convert to PNG
var svgDoc = SvgDocument.Open(cleanName);
var bitmap = svgDoc.Draw();
bitmap.Save(cleanName + ".png", ImageFormat.Png);

// Fetch Text Positions
Regex textInfoPattern = new Regex("\\<text class\\=\"[0-9a-z\\-]*\" transform\\=\"translate\\(([0-9]*.[0-9]*) ([0-9]*.[0-9]*)\\) rotate\\([\\-0-9]*\\)\"\\>([0-9]*)\\<\\/text>");
var matches = textInfoPattern.Matches(svgText);
foreach (Match m in matches)
{
    string Text = m.Groups[3].Value;
    int X = (int)Math.Round(double.Parse(m.Groups[1].Value));
    int Y = (int)Math.Round(double.Parse(m.Groups[2].Value));
    var e = heatPoints.FirstOrDefault(x => x.Room == Text);
    if (e != null)
    {
        int index = heatPoints.IndexOf(e);
        e.X = X;
        e.Y = Y-40;
        e.R = 60;
        heatPoints[index] = e;
    }
}

Mat Clean = Cv2.ImRead(cleanName+".png");

var dst = new Mat(Clean.Size(), MatType.CV_8UC1, new Scalar(0));

foreach (var hp in heatPoints)
{
    AddPoint(dst, new Point(hp.X, hp.Y), hp.R, hp.S);
}
dst.SaveImage("heatmap_points.jpg");
//Show(dst);
//ApplyFade(dst);
dst.SaveImage("heatmap_falloff.jpg");
//Show(dst);
ToHeatMap(dst);
Cv2.Add(dst, Clean, dst);
dst.SaveImage("heatmap_final.jpg");
Show(dst);

void AddPoint(Mat m, Point p, int r, float strength)
{
    int s = (int)Math.Round(255 * strength);
    Mat m2 = new(m.Size(), MatType.CV_8UC1, new Scalar(0));
    Cv2.Circle(m2, p, r, new Scalar(s, s, s), -1);

    Cv2.DistanceTransform(m2, m2, DistanceTypes.L2, DistanceTransformMasks.Precise, 3);
    Cv2.Normalize(m2, m2, 0, strength, NormTypes.MinMax);
    m2.ConvertTo(m2, MatType.CV_8UC1, 255, 0);

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

void Show(Mat m)
{
    Cv2.ImShow("View", m);
    Cv2.WaitKey();
}

class HeatPoint
{
    public string Room { get; set; }
    public int dBm { get; set; }
    public int Mbps { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int R { get; set; }
    public float S => (100 + dBm) * 0.025f;
}
