using System.Globalization;
using System.Text;

var ci = CultureInfo.InvariantCulture;
string dir = args[0];
string outDir = args[1];
Directory.CreateDirectory(outDir);
const double g = 9.81;
const double GapS = 0.020;

var traces = new List<Trace>();
foreach (var file in Directory.GetFiles(dir, "*.txt").OrderBy(f => f))
{
    var lines = File.ReadAllLines(file);
    var hdr = lines[0].Split(',').Select(s => s.Trim()).ToArray();
    int Col(string n) => Array.IndexOf(hdr, n);
    var rows = lines.Skip(1).Where(l => l.Length > 0).Select(l => l.Split(',')).ToList();
    double[] Get(string n) => rows.Select(r => double.Parse(r[Col(n)], ci)).ToArray();
    var tUs = Get("timeInUs_u32");
    var tr = new Trace
    {
        Name = file.Contains("_Brake_") ? "Brake" : "Throttle",
        T = tUs.Select(x => (x - tUs[0]) / 1e6).ToArray(),
        ForceN = Get("pedalForceFiltered_fl32").Select(x => x * g).ToArray(),
        Vel = Get("admittance_virtualVelocity_mps"),
        Volt = Get("servoVoltage_fl32"),
    };
    tr.Power = tr.ForceN.Zip(tr.Vel, (f, v) => f * v).ToArray();
    traces.Add(tr);
}

var sb = new StringBuilder();
foreach (var tr in traces)
{
    int n = tr.T.Length;
    double dur = tr.T[^1];
    double eRegen = 0, eMotor = 0, gapTotal = 0;
    for (int i = 1; i < n; i++)
    {
        double dt = tr.T[i] - tr.T[i - 1]; if (dt > GapS) { gapTotal += dt; continue; }
        double p = 0.5 * (tr.Power[i] + tr.Power[i - 1]);
        if (p > 0) eRegen += p * dt; else eMotor += -p * dt;
    }
    // 10 ms moving average of power (resistor sees bursts, short spikes are buffered by bus capacitors)
    double pPeak = tr.Power.Max();
    double pPeak10 = MovingAvgMax(tr.T, tr.Power, 0.010);
    double vMax = tr.Volt.Max(), vMin = tr.Volt.Min();
    double vNom = Median(tr.Volt);
    sb.AppendLine(string.Create(ci, $"{tr.Name};samples={n};duration_s={dur:F3};F_max_N={tr.ForceN.Max():F1};v_max_mps={tr.Vel.Max():F3};v_min_mps={tr.Vel.Min():F3};P_peak_W={pPeak:F1};P_peak_10ms_W={pPeak10:F1};P_min_W={tr.Power.Min():F1};E_regen_J={eRegen:F2};gap_total_s={gapTotal:F3};E_motor_J={eMotor:F2};P_regen_avg_W={eRegen / (dur - gapTotal):F2};V_min={vMin:F1};V_median={vNom:F1};V_max={vMax:F1};t_at_Pmax={tr.T[Array.IndexOf(tr.Power, pPeak)]:F3};t_at_Vmax={tr.T[Array.IndexOf(tr.Volt, vMax)]:F3}"));
}
File.WriteAllText(Path.Combine(outDir, "summary.txt"), sb.ToString());
Console.Write(sb);

// ---------- plots ----------
string blue = "#2a78d6";
double pMaxAbs = traces.Max(t => Math.Max(t.Power.Max(), -t.Power.Min()));
double pTop = Math.Ceiling(traces.Max(t => t.Power.Max()) / 50) * 50;
double pBot = Math.Floor(traces.Min(t => t.Power.Min()) / 50) * 50;
WritePanels(Path.Combine(outDir, "pedal_power.svg"), "Mechanical power P = F · v (positive = regenerative, energy flows into the servo bus)", "Power (W)",
    traces.Select(t => (t.Name, t.T, t.Power)).ToList(), pBot, pTop, 50, blue, refLine: null);

double fTop = Math.Ceiling(traces.Max(t => t.ForceN.Max()) / 100) * 100;
WritePanels(Path.Combine(outDir, "pedal_force.svg"), "Pedal force F (load cell, filtered)", "Force (N)",
    traces.Select(t => (t.Name, t.T, t.ForceN)).ToList(), 0, fTop, 100, blue, refLine: null);

double vTop = Math.Ceiling(traces.Max(t => t.Vel.Max()) * 10) / 10;
double vBot = Math.Floor(traces.Min(t => t.Vel.Min()) * 10) / 10;
WritePanels(Path.Combine(outDir, "pedal_velocity.svg"), "Pedal velocity v (admittance model)", "Velocity (m/s)",
    traces.Select(t => (t.Name, t.T, t.Vel)).ToList(), vBot, vTop, 0.2, blue, refLine: null);

WritePanels(Path.Combine(outDir, "servo_bus_voltage.svg"), "Measured servo bus voltage vs. 72 V limit", "Voltage (V)",
    traces.Select(t => (t.Name, t.T, t.Volt)).ToList(), 0, 80, 10, blue, refLine: (72, "72 V servo limit"));

Console.WriteLine("plots written");

static double Median(double[] a) { var s = a.OrderBy(x => x).ToArray(); return s[s.Length / 2]; }
static double MovingAvgMax(double[] t, double[] p, double win)
{
    double best = double.MinValue; int j = 0; double acc = 0;
    // time-weighted average over window ending at i
    for (int i = 1; i < t.Length; i++)
    {
        if (t[i] - t[i - 1] > GapS) { j = i; acc = 0; continue; }
        acc += 0.5 * (p[i] + p[i - 1]) * (t[i] - t[i - 1]);
        while (t[i] - t[j] > win) { acc -= 0.5 * (p[j + 1] + p[j]) * (t[j + 1] - t[j]); j++; }
        double span = t[i] - t[j];
        if (span > win * 0.9) best = Math.Max(best, acc / span);
    }
    return best;
}

static void WritePanels(string path, string title, string yLabel, List<(string name, double[] t, double[] y)> series,
    double yMin, double yMax, double yStep, string color, (double v, string label)? refLine)
{
    var ci = CultureInfo.InvariantCulture;
    int W = 960, panelH = 230, top = 56, gap = 34, left = 64, right = 24, bottomAxis = 40;
    int H = top + series.Count * panelH + (series.Count - 1) * gap + bottomAxis;
    double xMax = Math.Ceiling(series.Max(s => s.t[^1]));
    var s = new StringBuilder();
    string F(double d) => d.ToString("0.##", ci);
    s.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{W}\" height=\"{H}\" viewBox=\"0 0 {W} {H}\" font-family=\"Segoe UI, Helvetica, Arial, sans-serif\">");
    s.AppendLine($"<rect width=\"{W}\" height=\"{H}\" fill=\"#fcfcfb\"/>");
    s.AppendLine($"<text x=\"{left}\" y=\"26\" font-size=\"16\" font-weight=\"600\" fill=\"#0b0b0b\">{System.Security.SecurityElement.Escape(title)}</text>");
    for (int k = 0; k < series.Count; k++)
    {
        var (name, t, y) = series[k];
        int y0 = top + k * (panelH + gap);
        int pw = W - left - right;
        double X(double x) => left + x / xMax * pw;
        double Y(double v) => y0 + (yMax - v) / (yMax - yMin) * panelH;
        s.AppendLine($"<text x=\"{left}\" y=\"{y0 - 8}\" font-size=\"13\" font-weight=\"600\" fill=\"#52514e\">{name} pedal</text>");
        // grid + y ticks
        for (double v = yMin; v <= yMax + 1e-9; v += yStep)
        {
            double yy = Y(v);
            bool zero = Math.Abs(v) < 1e-9;
            s.AppendLine($"<line x1=\"{left}\" x2=\"{W - right}\" y1=\"{F(yy)}\" y2=\"{F(yy)}\" stroke=\"{(zero ? "#8a8984" : "#e4e3df")}\" stroke-width=\"1\"/>");
            s.AppendLine($"<text x=\"{left - 8}\" y=\"{F(yy + 4)}\" font-size=\"11\" text-anchor=\"end\" fill=\"#52514e\">{F(v)}</text>");
        }
        // x ticks
        for (double x = 0; x <= xMax + 1e-9; x += 0.5)
        {
            double xx = X(x);
            s.AppendLine($"<line x1=\"{F(xx)}\" x2=\"{F(xx)}\" y1=\"{y0}\" y2=\"{y0 + panelH}\" stroke=\"#f0efec\" stroke-width=\"1\"/>");
            if (k == series.Count - 1)
                s.AppendLine($"<text x=\"{F(xx)}\" y=\"{y0 + panelH + 18}\" font-size=\"11\" text-anchor=\"middle\" fill=\"#52514e\">{F(x)}</text>");
        }
        if (refLine is { } rl)
        {
            double yy = Y(rl.v);
            s.AppendLine($"<line x1=\"{left}\" x2=\"{W - right}\" y1=\"{F(yy)}\" y2=\"{F(yy)}\" stroke=\"#e34948\" stroke-width=\"2\" stroke-dasharray=\"6 4\"/>");
            s.AppendLine($"<text x=\"{W - right - 4}\" y=\"{F(yy - 6)}\" font-size=\"11\" text-anchor=\"end\" fill=\"#0b0b0b\">⚠ {rl.label}</text>");
        }
        // min/max decimation per pixel column
        var pts = new List<(double x, double y)>();
        int cols = pw;
        int idx = 0;
        for (int c = 0; c < cols && idx < t.Length; c++)
        {
            double tEnd = (c + 1) * xMax / cols;
            int start = idx; double mn = double.MaxValue, mx = double.MinValue; int imn = -1, imx = -1;
            while (idx < t.Length && t[idx] < tEnd) { if (y[idx] < mn) { mn = y[idx]; imn = idx; } if (y[idx] > mx) { mx = y[idx]; imx = idx; } idx++; }
            if (imn < 0) continue;
            if (imn <= imx) { pts.Add((t[imn], mn)); if (imx != imn) pts.Add((t[imx], mx)); }
            else { pts.Add((t[imx], mx)); pts.Add((t[imn], mn)); }
        }
        var d = new StringBuilder();
        for (int i = 0; i < pts.Count; i++) d.Append(i == 0 || pts[i].x - pts[i - 1].x > GapS ? "M" : "L").Append(F(X(pts[i].x))).Append(',').Append(F(Y(Math.Clamp(pts[i].y, yMin, yMax))));
        s.AppendLine($"<path d=\"{d}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"1.5\" stroke-linejoin=\"round\"/>");
        // peak annotation
        int ip = Array.IndexOf(y, y.Max());
        s.AppendLine($"<circle cx=\"{F(X(t[ip]))}\" cy=\"{F(Y(y[ip]))}\" r=\"4\" fill=\"{color}\" stroke=\"#fcfcfb\" stroke-width=\"2\"/>");
        bool leftSide = X(t[ip]) > W - 200;
        s.AppendLine($"<text x=\"{F(X(t[ip]) + (leftSide ? -8 : 8))}\" y=\"{F(Y(y[ip]) + 4)}\" font-size=\"11\" text-anchor=\"{(leftSide ? "end" : "start")}\" fill=\"#0b0b0b\">max {y[ip].ToString(Math.Abs(y[ip]) < 10 ? "0.00" : "0.#", ci)}</text>");
    }
    int yAxisMid = top + (series.Count * panelH + (series.Count - 1) * gap) / 2;
    s.AppendLine($"<text transform=\"translate(16,{yAxisMid}) rotate(-90)\" font-size=\"12\" text-anchor=\"middle\" fill=\"#52514e\">{yLabel}</text>");
    s.AppendLine($"<text x=\"{left + (W - left - right) / 2}\" y=\"{H - 6}\" font-size=\"12\" text-anchor=\"middle\" fill=\"#52514e\">Time (s)</text>");
    s.AppendLine("</svg>");
    File.WriteAllText(path, s.ToString());
}

class Trace
{
    public string Name = "";
    public double[] T = [], ForceN = [], Vel = [], Volt = [], Power = [];
}
