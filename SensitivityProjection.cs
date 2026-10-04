using System;
using System.Globalization;
using System.Linq;

namespace NexaArena
{
    internal sealed class SensitivityResolution
    {
        public readonly int Width, Height;
        public SensitivityResolution(int width, int height) { Width=width; Height=height; }
        public string Key { get { return Width+"x"+Height; } }
        public double Aspect { get { return (double)Width/Height; } }
        public string Ratio
        {
            get
            {
                if(Width*10==Height*16)return "16:10";
                int a=Width,b=Height;while(b!=0){int next=a%b;a=b;b=next;}
                return (Width/a)+":"+(Height/a);
            }
        }
        public override string ToString() { return Width+" × "+Height+"  ·  "+Ratio; }
    }

    // All presets assume an image stretched across the same monitor's full width.
    // MDH 0% matches horizontal motion at screen centre, not cm/360 or every flick distance.
    // CS2: 90 horizontal degrees at 4:3. VALORANT true stretch: 103 at 16:9,
    // preserving vertical FOV. Ordinary VALORANT HUD-only stretch is NOT this model.
    internal static class SensitivityProjection
    {
        public static readonly SensitivityResolution[] Presets = {
            new SensitivityResolution(1920,1080), new SensitivityResolution(2560,1440),
            new SensitivityResolution(1280,720), new SensitivityResolution(1600,900), new SensitivityResolution(3840,2160),
            new SensitivityResolution(1024,768), new SensitivityResolution(1152,864),
            new SensitivityResolution(1280,960), new SensitivityResolution(1440,1080), new SensitivityResolution(1920,1440),
            new SensitivityResolution(1280,1024), new SensitivityResolution(1350,1080), new SensitivityResolution(1600,1280),
            new SensitivityResolution(1280,800), new SensitivityResolution(1600,1000), new SensitivityResolution(1680,1050),
            new SensitivityResolution(1728,1080), new SensitivityResolution(1920,1200),
            new SensitivityResolution(1568,1080), new SensitivityResolution(2088,1440)
        };

        public static SensitivityResolution Resolve(string key)
        {
            SensitivityResolution resolution=Presets.FirstOrDefault(r=>r.Key==key);
            if(resolution==null)throw new ArgumentException("请选择列表中的常用分辨率。");
            return resolution;
        }
        private static double HalfFovTangent(string game,SensitivityResolution resolution)
        {
            if(resolution==null||resolution.Width<320||resolution.Height<200)throw new ArgumentException("分辨率无效。");
            if(game=="CS2")return resolution.Aspect/(4.0/3.0);
            if(game=="VALORANT")return Math.Tan(103.0*Math.PI/360.0)*resolution.Aspect/(16.0/9.0);
            throw new ArgumentException("未知游戏。");
        }
        public static double HorizontalFov(string game,SensitivityResolution resolution)
        {
            return 2*Math.Atan(HalfFovTangent(game,resolution))*180/Math.PI;
        }
        public static double Convert(string source,string target,SensitivityResolution sourceResolution,
            SensitivityResolution targetResolution,double sensitivity,double sourceDpi,double targetDpi)
        {
            double turnRate=SensitivityMath.Convert(source,target,sensitivity,sourceDpi,targetDpi);
            return turnRate*HalfFovTangent(target,targetResolution)/HalfFovTangent(source,sourceResolution);
        }
        public static string DisplayNumber(double value)
        {
            string rounded=value.ToString("0.######",CultureInfo.InvariantCulture);
            return rounded=="0"&&value>0?SensitivityMath.Format(value):rounded;
        }
    }

    internal static class SensitivityFormulas
    {
        public const string TurnDistance = "turn-distance";
        public const string ScreenCenter = "screen-center";
        public const string CustomRatio = "custom-ratio";
        public static readonly string[] Ids = { TurnDistance, ScreenCenter, CustomRatio };
        public static readonly string[] Names = { "等转身距离（cm/360）", "水平微调匹配（MDH 0%）", "自定义经验比例" };

        public static void Validate(string method, double customCsPerValorant)
        {
            if (!Ids.Contains(method)) throw new ArgumentException("请选择有效的换算方案。");
            if (double.IsNaN(customCsPerValorant) || double.IsInfinity(customCsPerValorant) ||
                customCsPerValorant < 0.01 || customCsPerValorant > 100)
                throw new ArgumentException("经验比值应在 0.01–100 之间。");
        }

        public static double Convert(string method, string source, string target,
            SensitivityResolution from, SensitivityResolution to, double sensitivity,
            double sourceDpi, double targetDpi, double customCsPerValorant)
        {
            Validate(method, customCsPerValorant);
            if (method == ScreenCenter)
                return SensitivityProjection.Convert(source, target, from, to, sensitivity, sourceDpi, targetDpi);
            if (method == TurnDistance)
                return SensitivityMath.Convert(source, target, sensitivity, sourceDpi, targetDpi);

            // This number is always CS / VALORANT at the SAME DPI, regardless of UI direction.
            // It is an empirical replacement for (not an extra multiplier on) yaw/FOV conversion.
            double dpiAdjusted = SensitivityMath.Convert(source, source, sensitivity, sourceDpi, targetDpi);
            SensitivityMath.Yaw(target); // Validate the second game even in the custom branch.
            if (source == target) return dpiAdjusted;
            return source == "CS2" ? dpiAdjusted / customCsPerValorant : dpiAdjusted * customCsPerValorant;
        }
    }
}
