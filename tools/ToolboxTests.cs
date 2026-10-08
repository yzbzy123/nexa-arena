using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace NexaArena
{
    internal static class ToolboxTests
    {
        private static int count;
        private static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
        private static void Equal(double actual,double expected){Check(Math.Abs(actual-expected)<1e-8,"Expected "+expected+", got "+actual);}
        private static void Throws(Action action){try{action();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}throw new Exception("Expected validation failure");}
        private static void BackupFailure(Action action){try{action();}catch(IOException){return;}catch(UnauthorizedAccessException){return;}throw new Exception("Expected backup persistence failure");}
        private static void Test(string name,Action action){action();count++;Console.WriteLine("PASS "+name);}
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                Test("CS2 to VALORANT conversion",delegate{Equal(SensitivityMath.Convert("CS2","VALORANT",1,800,800),0.022/0.07);});
                Test("round trip sensitivity",delegate{double v=SensitivityMath.Convert("VALORANT","CS2",0.35,1600,800);Equal(SensitivityMath.Convert("CS2","VALORANT",v,800,1600),0.35);});
                Test("DPI changes preserve physical cm360",delegate{double v=SensitivityMath.Convert("CS2","CS2",2,800,1600);Equal(v,1);Equal(SensitivityMath.Cm360("CS2",2,800),SensitivityMath.Cm360("CS2",v,1600));});
                Test("small valid sensitivity is not copied as zero",delegate{double value=SensitivityMath.Convert("CS2","VALORANT",0.000001,50,100000);Check(double.Parse(SensitivityMath.Format(value),CultureInfo.InvariantCulture)>0,"rounding lost the result");});
                Test("reject nonpositive and nonfinite sensitivity",delegate{foreach(double v in new[]{0,-1,double.NaN,double.PositiveInfinity})Throws(delegate{SensitivityMath.Convert("CS2","VALORANT",v,800,800);});});
                Test("CS2 field of view follows stretched aspect ratio",delegate
                {
                    Equal(SensitivityProjection.HorizontalFov("CS2",SensitivityProjection.Resolve("1280x960")),90);
                    Equal(SensitivityProjection.HorizontalFov("CS2",SensitivityProjection.Resolve("1920x1080")),106.26020470831196);
                });
                Test("VALORANT true stretch keeps vertical field of view",delegate
                {
                    Equal(SensitivityProjection.HorizontalFov("VALORANT",SensitivityProjection.Resolve("1920x1080")),103);
                    Equal(SensitivityProjection.HorizontalFov("VALORANT",SensitivityProjection.Resolve("1280x960")),86.63197087385085);
                });
                Test("16:9 visual micro-aim conversion is not the cm360 ratio",delegate
                {
                    var res=SensitivityProjection.Resolve("1920x1080");
                    Equal(SensitivityProjection.Convert("CS2","VALORANT",res,res,1,800,800),0.2963334704594678);
                });
                Test("CS2 4:3 to VALORANT 16:9 uses the matching projection ratio",delegate
                {
                    Equal(SensitivityProjection.Convert("CS2","VALORANT",SensitivityProjection.Resolve("1280x960"),SensitivityProjection.Resolve("1920x1080"),1,800,800),0.395111293945957);
                });
                Test("4:3 to 5:4 stretched changes sensitivity even within one game",delegate
                {
                    Equal(SensitivityProjection.Convert("CS2","CS2",SensitivityProjection.Resolve("1280x960"),SensitivityProjection.Resolve("1280x1024"),1,800,800),0.9375);
                });
                Test("same aspect ratio at different pixel counts has no added factor",delegate
                {
                    var a=SensitivityProjection.Resolve("1280x960");var b=SensitivityProjection.Resolve("1440x1080");var to=SensitivityProjection.Resolve("1920x1080");
                    Equal(SensitivityProjection.Convert("CS2","VALORANT",a,to,1.2,800,800),SensitivityProjection.Convert("CS2","VALORANT",b,to,1.2,800,800));
                    Equal(SensitivityProjection.Convert("CS2","CS2",a,b,1.2,800,800),1.2);
                });
                Test("visual matching round trips and accounts for different DPI",delegate
                {
                    var a=SensitivityProjection.Resolve("1568x1080");var b=SensitivityProjection.Resolve("1280x1024");
                    double converted=SensitivityProjection.Convert("VALORANT","CS2",a,b,0.45,1600,800);
                    Equal(SensitivityProjection.Convert("CS2","VALORANT",b,a,converted,800,1600),0.45);
                });
                Test("all preset pairs preserve centre screen motion in the declared model",delegate
                {
                    foreach(var a in SensitivityProjection.Presets)foreach(var b in SensitivityProjection.Presets)
                    {
                        double converted=SensitivityProjection.Convert("CS2","VALORANT",a,b,1.5,800,1600);
                        double source=800*1.5*0.022/Math.Tan(SensitivityProjection.HorizontalFov("CS2",a)*Math.PI/360);
                        double target=1600*converted*0.07/Math.Tan(SensitivityProjection.HorizontalFov("VALORANT",b)*Math.PI/360);
                        Equal(source,target);
                    }
                });
                Test("resolution validation and small result formatting",delegate
                {
                    Throws(delegate{SensitivityProjection.Resolve("invalid");});
                    Throws(delegate{SensitivityProjection.HorizontalFov("CS2",new SensitivityResolution(0,0));});
                    Check(SensitivityProjection.DisplayNumber(0.00000000015)!="0","valid output rounded to zero");
                    Check(SensitivityProjection.DisplayNumber(0.395111293945957)=="0.395111","unexpected display precision");
                    Check(SensitivityProjection.Resolve("1280x800").Ratio=="16:10","nonstandard aspect label");
                });
                Test("turn-distance method preserves the original physical conversion",delegate
                {
                    foreach(string source in new[]{"CS2","VALORANT"})foreach(string target in new[]{"CS2","VALORANT"})
                    foreach(var from in SensitivityProjection.Presets)foreach(var to in SensitivityProjection.Presets)
                    {
                        double actual=SensitivityFormulas.Convert("turn-distance",source,target,from,to,0.173,1600,800,3.5);
                        Equal(actual,SensitivityMath.Convert(source,target,0.173,1600,800));
                        Equal(SensitivityMath.Cm360(source,0.173,1600),SensitivityMath.Cm360(target,actual,800));
                    }
                });
                Test("screen-center method preserves the original projection conversion",delegate
                {
                    foreach(string source in new[]{"CS2","VALORANT"})foreach(string target in new[]{"CS2","VALORANT"})
                    foreach(var from in SensitivityProjection.Presets)foreach(var to in SensitivityProjection.Presets)
                        Equal(SensitivityFormulas.Convert("screen-center",source,target,from,to,0.173,1600,800,3.5),
                            SensitivityProjection.Convert(source,target,from,to,0.173,1600,800));
                });
                Test("custom ratio 3.5 converts VALORANT 0.173 to CS2 0.6055",delegate
                {
                    var from=SensitivityProjection.Resolve("1280x960");var to=SensitivityProjection.Resolve("1920x1080");
                    Equal(SensitivityFormulas.Convert("custom-ratio","VALORANT","CS2",from,to,0.173,800,800,3.5),0.6055);
                    Equal(SensitivityFormulas.Convert("custom-ratio","CS2","VALORANT",to,from,0.6055,800,800,3.5),0.173);
                });
                Test("custom ratio direction and DPI adjustment round trip at ratio limits",delegate
                {
                    var from=SensitivityProjection.Resolve("1568x1080");var to=SensitivityProjection.Resolve("1280x1024");
                    foreach(double ratio in new[]{0.01,3.5,100})
                    {
                        double cs=SensitivityFormulas.Convert("custom-ratio","VALORANT","CS2",from,to,0.173,1600,800,ratio);
                        Equal(cs,0.173*2*ratio);
                        Equal(SensitivityFormulas.Convert("custom-ratio","CS2","VALORANT",to,from,cs,800,1600,ratio),0.173);
                        double val=SensitivityFormulas.Convert("custom-ratio","CS2","VALORANT",from,to,1.2,800,1600,ratio);
                        Equal(val,1.2*0.5/ratio);
                        Equal(SensitivityFormulas.Convert("custom-ratio","VALORANT","CS2",to,from,val,1600,800,ratio),1.2);
                    }
                });
                Test("custom ratio within one game only scales by DPI",delegate
                {
                    var from=SensitivityProjection.Resolve("1280x960");var to=SensitivityProjection.Resolve("1280x1024");
                    foreach(string game in new[]{"CS2","VALORANT"})foreach(double ratio in new[]{0.01,3.5,100})
                    {
                        Equal(SensitivityFormulas.Convert("custom-ratio",game,game,from,to,0.173,800,800,ratio),0.173);
                        Equal(SensitivityFormulas.Convert("custom-ratio",game,game,from,to,0.173,1600,800,ratio),0.346);
                        Equal(SensitivityFormulas.Convert("custom-ratio",game,game,to,from,0.173,800,1600,ratio),0.0865);
                    }
                });
                Test("every formula scales independently with source and target DPI",delegate
                {
                    var from=SensitivityProjection.Resolve("1280x960");var to=SensitivityProjection.Resolve("1920x1080");
                    foreach(string method in new[]{"turn-distance","screen-center","custom-ratio"})
                    foreach(string source in new[]{"CS2","VALORANT"})
                    {
                        string target=source=="CS2"?"VALORANT":"CS2";
                        double baseline=SensitivityFormulas.Convert(method,source,target,from,to,0.173,800,800,3.5);
                        Equal(SensitivityFormulas.Convert(method,source,target,from,to,0.173,1600,800,3.5),baseline*2);
                        Equal(SensitivityFormulas.Convert(method,source,target,from,to,0.173,800,1600,3.5),baseline/2);
                        Equal(SensitivityFormulas.Convert(method,source,target,from,to,0.173,1600,1600,3.5),baseline);
                    }
                });
                Test("resolution choices affect only the screen-center method",delegate
                {
                    var wide=SensitivityProjection.Resolve("1920x1080");var narrow=SensitivityProjection.Resolve("1280x960");
                    foreach(string source in new[]{"CS2","VALORANT"})
                    {
                        string target=source=="CS2"?"VALORANT":"CS2";
                        foreach(string method in new[]{"turn-distance","custom-ratio"})
                        {
                            double baseline=SensitivityFormulas.Convert(method,source,target,wide,wide,0.173,800,800,3.5);
                            Equal(SensitivityFormulas.Convert(method,source,target,narrow,wide,0.173,800,800,3.5),baseline);
                            Equal(SensitivityFormulas.Convert(method,source,target,wide,narrow,0.173,800,800,3.5),baseline);
                        }
                        double centre=SensitivityFormulas.Convert("screen-center",source,target,wide,wide,0.173,800,800,3.5);
                        Equal(SensitivityFormulas.Convert("screen-center",source,target,narrow,wide,0.173,800,800,3.5),centre*4/3);
                        Equal(SensitivityFormulas.Convert("screen-center",source,target,wide,narrow,0.173,800,800,3.5),centre*3/4);
                    }
                });
                Test("custom ratio never changes either built-in method",delegate
                {
                    var from=SensitivityProjection.Resolve("1280x960");var to=SensitivityProjection.Resolve("1920x1080");
                    foreach(string method in new[]{"turn-distance","screen-center"})
                    foreach(string source in new[]{"CS2","VALORANT"})
                    {
                        string target=source=="CS2"?"VALORANT":"CS2";
                        Equal(SensitivityFormulas.Convert(method,source,target,from,to,0.173,800,1600,0.01),
                            SensitivityFormulas.Convert(method,source,target,from,to,0.173,800,1600,100));
                    }
                });
                Test("formula validation accepts known methods and inclusive ratio boundaries",delegate
                {
                    foreach(string method in new[]{"turn-distance","screen-center","custom-ratio"})
                    foreach(double ratio in new[]{0.01,3.5,100})SensitivityFormulas.Validate(method,ratio);
                });
                Test("formula validation rejects unknown methods and invalid custom ratios",delegate
                {
                    var res=SensitivityProjection.Resolve("1920x1080");
                    foreach(string method in new[]{null,"","unknown"," custom-ratio","CUSTOM-RATIO"})
                    {
                        Throws(delegate{SensitivityFormulas.Validate(method,3.5);});
                        Throws(delegate{SensitivityFormulas.Convert(method,"CS2","VALORANT",res,res,1,800,800,3.5);});
                    }
                    foreach(double ratio in new[]{-1,0,0.009,100.001,double.NaN,double.NegativeInfinity,double.PositiveInfinity})
                    {
                        Throws(delegate{SensitivityFormulas.Validate("custom-ratio",ratio);});
                        Throws(delegate{SensitivityFormulas.Convert("custom-ratio","CS2","VALORANT",res,res,1,800,800,ratio);});
                    }
                });
                Test("every formula rejects invalid sensitivity DPI and game names",delegate
                {
                    var res=SensitivityProjection.Resolve("1920x1080");
                    foreach(string method in new[]{"turn-distance","screen-center","custom-ratio"})
                    {
                        foreach(double value in new[]{0,-1,double.NaN,double.NegativeInfinity,double.PositiveInfinity})
                        {
                            Throws(delegate{SensitivityFormulas.Convert(method,"CS2","VALORANT",res,res,value,800,800,3.5);});
                            Throws(delegate{SensitivityFormulas.Convert(method,"CS2","VALORANT",res,res,1,value,800,3.5);});
                            Throws(delegate{SensitivityFormulas.Convert(method,"CS2","VALORANT",res,res,1,800,value,3.5);});
                        }
                        Throws(delegate{SensitivityFormulas.Convert(method,"unknown","VALORANT",res,res,1,800,800,3.5);});
                        Throws(delegate{SensitivityFormulas.Convert(method,"CS2","unknown",res,res,1,800,800,3.5);});
                    }
                });
                Test("sensitivity page first calculation works without showing a window",delegate
                {
                    using(var probe=new SensitivityPageProbe())
                    {
                        Check(probe.Formula.SelectedIndex==Array.IndexOf(SensitivityFormulas.Ids,"screen-center"),"page did not select the default formula");
                        Check(probe.Result.Text=="VALORANT  0.296333","constructor did not calculate the initial result");
                        probe.CheckState("screen-center");
                        Console.WriteLine("Hidden custom label GetControlFromPosition(0,3)="+(probe.InputGrid.GetControlFromPosition(0,3)==null?"null":"control"));
                        Check(probe.Saved.Count==0,"page construction unexpectedly saved settings");
                    }
                });
                Test("sensitivity page switches three formulas and recalculates the result",delegate
                {
                    using(var probe=new SensitivityPageProbe())
                    {
                        probe.Source.SelectedItem="VALORANT";probe.Target.SelectedItem="CS2";probe.Sensitivity.Value=0.173m;
                        probe.SelectFormula("turn-distance");string turn=probe.Result.Text;
                        probe.SelectFormula("screen-center");string centre=probe.Result.Text;
                        probe.SelectFormula("custom-ratio");
                        Check(probe.Result.Text=="CS2  0.6055","custom ratio page example is incorrect");
                        Check(turn!=centre&&turn!=probe.Result.Text&&centre!=probe.Result.Text,"changing the formula did not update the displayed result");
                        Check(probe.Saved.Count==0,"formula selection unexpectedly saved settings");
                    }
                });
                Test("sensitivity page custom input and game changes update control state",delegate
                {
                    using(var probe=new SensitivityPageProbe())
                    {
                        probe.SelectFormula("custom-ratio");
                        probe.Source.SelectedItem="VALORANT";probe.Target.SelectedItem="CS2";probe.Sensitivity.Value=0.173m;
                        probe.CustomRatio.Value=4;probe.CheckState("custom-ratio");Check(probe.Result.Text=="CS2  0.692","editing custom ratio did not recalculate");
                        probe.Target.SelectedItem="VALORANT";probe.CheckState("custom-ratio");Check(probe.Result.Text=="VALORANT  0.173","same game incorrectly uses custom ratio");
                        probe.Dpi.Value=1600;probe.CheckState("custom-ratio");Check(probe.Result.Text=="VALORANT  0.346","same game did not update for source DPI");
                        probe.TargetDpi.Value=1600;probe.CheckState("custom-ratio");Check(probe.Result.Text=="VALORANT  0.173","same game did not update for target DPI");
                        probe.Source.SelectedItem="CS2";probe.Sensitivity.Value=0.692m;probe.CheckState("custom-ratio");Check(probe.Result.Text=="VALORANT  0.173","reverse custom ratio page direction is incorrect");
                        probe.Target.SelectedItem="CS2";probe.CheckState("custom-ratio");Check(probe.Result.Text=="CS2  0.692","CS2 same-game result is incorrect");
                        probe.Target.SelectedItem="VALORANT";probe.CheckState("custom-ratio");
                        Check(probe.CustomRatio.Enabled,"custom ratio did not re-enable after leaving same-game conversion");
                        Check(probe.Saved.Count==0,"editing inputs unexpectedly saved settings");
                    }
                });
                Test("sensitivity page enables and applies resolutions only for screen-center",delegate
                {
                    using(var probe=new SensitivityPageProbe())
                    {
                        foreach(string method in new[]{"turn-distance","screen-center","custom-ratio"})
                        {
                            probe.SelectFormula(method);
                            probe.SourceResolution.SelectedItem=SensitivityProjection.Resolve("1920x1080");
                            probe.TargetResolution.SelectedItem=SensitivityProjection.Resolve("1920x1080");string baseline=probe.Result.Text;
                            probe.SourceResolution.SelectedItem=SensitivityProjection.Resolve("1280x960");probe.CheckState(method);
                            Check((probe.Result.Text!=baseline)==(method=="screen-center"),"source resolution affected the wrong method");
                            probe.SourceResolution.SelectedItem=SensitivityProjection.Resolve("1920x1080");
                            probe.TargetResolution.SelectedItem=SensitivityProjection.Resolve("1280x1024");probe.CheckState(method);
                            Check((probe.Result.Text!=baseline)==(method=="screen-center"),"target resolution affected the wrong method");
                        }
                        Check(probe.Saved.Count==0,"resolution changes unexpectedly saved settings");
                    }
                });
                Test("sensitivity page repeatedly hides and restores custom row without layout errors",delegate
                {
                    using(var probe=new SensitivityPageProbe())
                    {
                        int hiddenLookups=0,hiddenNulls=0;
                        for(int cycle=0;cycle<12;cycle++)foreach(string method in new[]{"custom-ratio","screen-center","turn-distance","screen-center","custom-ratio"})
                        {
                            probe.SelectFormula(method);probe.Sensitivity.Value=0.173m+cycle*0.01m;
                            probe.Page.Size=new Size(cycle%2==0?900:1200,900);probe.CheckState(method);
                            Check(probe.InputGrid.Controls.Contains(probe.CustomLabel),"hiding the row removed its label");
                            if(method=="custom-ratio")Check(probe.InputGrid.GetControlFromPosition(0,3)==probe.CustomLabel,"custom label did not return to its original cell");
                            else{hiddenLookups++;if(probe.InputGrid.GetControlFromPosition(0,3)==null)hiddenNulls++;}
                        }
                        Console.WriteLine("Repeated hidden custom label lookups="+hiddenLookups+"; null results="+hiddenNulls);
                        Check(probe.Saved.Count==0,"repeated layout or formula changes unexpectedly saved settings");
                    }
                });
                Test("swap direction carries result and exchanges resolutions and DPI without saving",delegate
                {
                    foreach(string method in SensitivityFormulas.Ids)
                    using(var probe=new SensitivityPageProbe())
                    {
                        probe.SelectFormula(method);probe.Source.SelectedItem="VALORANT";probe.Target.SelectedItem="CS2";
                        probe.SourceResolution.SelectedIndex=2;probe.TargetResolution.SelectedIndex=3;
                        object beforeSource=probe.SourceResolution.SelectedItem,beforeTarget=probe.TargetResolution.SelectedItem;
                        probe.Dpi.Value=800;probe.TargetDpi.Value=1600;probe.Sensitivity.Value=0.173m;
                        double previous=(double)typeof(SensitivityPage).GetField("converted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(probe.Page);
                        typeof(SensitivityPage).GetMethod("SwapDirection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(probe.Page,null);
                        Check((string)probe.Source.SelectedItem=="CS2"&&(string)probe.Target.SelectedItem=="VALORANT","games not exchanged");
                        Check(probe.SourceResolution.SelectedItem==beforeTarget&&probe.TargetResolution.SelectedItem==beforeSource,"resolutions not exchanged");
                        Check(probe.Dpi.Value==1600&&probe.TargetDpi.Value==800,"DPI not exchanged");
                        Check(probe.Sensitivity.Value==decimal.Round((decimal)previous,6),"result not used as reverse input");
                        double reverse=(double)typeof(SensitivityPage).GetField("converted",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(probe.Page);
                        Check(Math.Abs(reverse-0.173)<0.00001,"reverse conversion lost the original sensitivity");
                        Check(probe.Saved.Count==0,"swap unexpectedly persisted settings");
                    }
                });
                Test("out of range reverse conversion leaves all inputs unchanged",delegate
                {
                    using(var probe=new SensitivityPageProbe())
                    {
                        probe.SelectFormula("custom-ratio");probe.Source.SelectedItem="VALORANT";probe.Target.SelectedItem="CS2";probe.Sensitivity.Value=100;probe.CustomRatio.Value=100;
                        try {typeof(SensitivityPage).GetMethod("SwapDirection",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(probe.Page,null);throw new Exception("expected rejection");}
                        catch(TargetInvocationException ex){Check(ex.InnerException is InvalidOperationException,"wrong rejection");}
                        Check((string)probe.Source.SelectedItem=="VALORANT"&&probe.Sensitivity.Value==100,"partial swap changed inputs");
                        Check(probe.Saved.Count==0,"failed swap persisted input");
                    }
                });
                Test("legacy settings keep input and hotkeys with default resolution presets",delegate
                {
                    var value=new ToolboxSettings {Sensitivity=0.6m,SourceDpi=1600};value.Hotkeys[0].Enabled=false;
                    var serializer=new XmlSerializer(typeof(ToolboxSettings));string xml;
                    using(var writer=new StringWriter()){serializer.Serialize(writer,value);xml=writer.ToString();}
                    xml=System.Text.RegularExpressions.Regex.Replace(xml,@"<(?:SourceResolution|TargetResolution|SensitivityMethod|CustomCsPerValorant)>.*?</(?:SourceResolution|TargetResolution|SensitivityMethod|CustomCsPerValorant)>","");
                    using(var reader=new StringReader(xml))
                    {
                        var loaded=(ToolboxSettings)serializer.Deserialize(reader);ToolboxSettings.Validate(loaded);
                        Check(loaded.SourceResolution=="1920x1080"&&loaded.TargetResolution=="1920x1080"&&loaded.Sensitivity==0.6m&&loaded.SourceDpi==1600&&!loaded.Hotkeys[0].Enabled,"legacy settings changed");
                        Check(loaded.SensitivityMethod=="screen-center"&&loaded.CustomCsPerValorant==3.5m,"legacy formula defaults changed");
                    }
                });
                Test("pre-formula XML keeps resolution input and every hotkey field",delegate
                {
                    var value=new ToolboxSettings {MinimizeToTray=false,SourceGame="VALORANT",TargetGame="CS2",SourceResolution="1568x1080",TargetResolution="1280x1024",Sensitivity=0.173m,SourceDpi=1600,TargetDpi=400,SensitivityMethod="custom-ratio",CustomCsPerValorant=4.25m};
                    value.Hotkeys[0].Enabled=false;value.Hotkeys[1].Key=70;value.Hotkeys[1].Modifiers=6;value.Hotkeys[3].Enabled=true;
                    var serializer=new XmlSerializer(typeof(ToolboxSettings));string xml;
                    using(var writer=new StringWriter()){serializer.Serialize(writer,value);xml=writer.ToString();}
                    xml=System.Text.RegularExpressions.Regex.Replace(xml,@"<(?:SensitivityMethod|CustomCsPerValorant)>.*?</(?:SensitivityMethod|CustomCsPerValorant)>","");
                    using(var reader=new StringReader(xml))
                    {
                        var loaded=(ToolboxSettings)serializer.Deserialize(reader);ToolboxSettings.Validate(loaded);
                        Check(loaded.SensitivityMethod=="screen-center"&&loaded.CustomCsPerValorant==3.5m,"missing fields did not retain defaults");
                        Check(!loaded.MinimizeToTray&&loaded.SourceGame=="VALORANT"&&loaded.TargetGame=="CS2","legacy game or tray input lost");
                        Check(loaded.SourceResolution=="1568x1080"&&loaded.TargetResolution=="1280x1024"&&loaded.Sensitivity==0.173m&&loaded.SourceDpi==1600&&loaded.TargetDpi==400,"legacy resolution or sensitivity input lost");
                        Check(loaded.Hotkeys.Count==value.Hotkeys.Count,"legacy hotkeys duplicated or lost");
                        for(int i=0;i<value.Hotkeys.Count;i++)Check(loaded.Hotkeys[i].Action==value.Hotkeys[i].Action&&loaded.Hotkeys[i].Enabled==value.Hotkeys[i].Enabled&&loaded.Hotkeys[i].Modifiers==value.Hotkeys[i].Modifiers&&loaded.Hotkeys[i].Key==value.Hotkeys[i].Key,"legacy hotkey changed at "+i);
                    }
                });
                Test("all formula settings and custom ratio values survive XML round trips",delegate
                {
                    var serializer=new XmlSerializer(typeof(ToolboxSettings));
                    foreach(string method in new[]{"turn-distance","screen-center","custom-ratio"})
                    foreach(decimal ratio in new[]{0.01m,3.5m,4.25m,100m})
                    {
                        var value=new ToolboxSettings {SensitivityMethod=method,CustomCsPerValorant=ratio,Sensitivity=0.173m,SourceDpi=1600,TargetDpi=400,SourceResolution="1280x960",TargetResolution="1920x1080"};
                        value.Hotkeys[0].Enabled=false;value.Hotkeys[1].Key=70;
                        using(var buffer=new MemoryStream())
                        {
                            serializer.Serialize(buffer,value);buffer.Position=0;var loaded=(ToolboxSettings)serializer.Deserialize(buffer);ToolboxSettings.Validate(loaded);
                            Check(loaded.SensitivityMethod==method&&loaded.CustomCsPerValorant==ratio,"formula settings roundtrip mismatch");
                            Check(loaded.Sensitivity==0.173m&&loaded.SourceDpi==1600&&loaded.TargetDpi==400&&loaded.SourceResolution=="1280x960"&&loaded.TargetResolution=="1920x1080","sensitivity input roundtrip mismatch");
                            Check(loaded.Hotkeys.Count==4&&!loaded.Hotkeys[0].Enabled&&loaded.Hotkeys[1].Key==70,"formula roundtrip changed hotkeys");
                        }
                    }
                });
                Test("settings validation rejects invalid method and custom ratio",delegate
                {
                    foreach(string method in new[]{null,"","unknown"})Throws(delegate{ToolboxSettings.Validate(new ToolboxSettings {SensitivityMethod=method});});
                    foreach(decimal ratio in new[]{-1m,0m,0.009m,100.001m})Throws(delegate{ToolboxSettings.Validate(new ToolboxSettings {SensitivityMethod="custom-ratio",CustomCsPerValorant=ratio});});
                });
                Test("settings XML round trip does not duplicate hotkeys",delegate
                {
                    var value=new ToolboxSettings {Sensitivity=0.35m,SourceDpi=1600};var serializer=new XmlSerializer(typeof(ToolboxSettings));
                    using(var buffer=new MemoryStream()){serializer.Serialize(buffer,value);buffer.Position=0;var loaded=(ToolboxSettings)serializer.Deserialize(buffer);ToolboxSettings.Validate(loaded);Check(loaded.Hotkeys.Count==4&&loaded.Sensitivity==0.35m,"roundtrip mismatch");}
                });
                Test("settings copies preserve formulas without mutating active inputs or hotkeys",delegate{var before=new ToolboxSettings {SensitivityMethod="custom-ratio",CustomCsPerValorant=4.25m};var after=before.Copy();Check(after.SensitivityMethod=="custom-ratio"&&after.CustomCsPerValorant==4.25m,"formula copy mismatch");after.Hotkeys[0].Key=70;after.SensitivityMethod="turn-distance";after.CustomCsPerValorant=3.5m;Check(before.Hotkeys[0].Key==83&&before.SensitivityMethod=="custom-ratio"&&before.CustomCsPerValorant==4.25m,"copy mutated active settings");});
                Test("duplicate hotkeys rejected before any registration",delegate{var api=new FakeKeys();using(var manager=new HotkeyManager(api)){var keys=ToolboxSettings.Defaults();keys[1].Key=keys[0].Key;Throws(delegate{manager.Apply(keys);});Check(api.Calls==0,"native API called on invalid settings");}});
                Test("unmodified game keys and reserved Alt F4 rejected",delegate{var keys=ToolboxSettings.Defaults();keys[0].Modifiers=0;Throws(delegate{HotkeyRules.Validate(keys);});keys[0].Modifiers=1;keys[0].Key=115;Throws(delegate{HotkeyRules.Validate(keys);});});
                Test("registration conflict restores original bindings",delegate
                {
                    var api=new FakeKeys();using(var manager=new HotkeyManager(api)){var original=ToolboxSettings.Defaults();manager.Apply(original);var next=original.Select(k=>k.Copy()).ToList();next[0].Key=90;api.BlockedKey=90;Throws(delegate{manager.Apply(next);});Check(manager.ActionFor(0x6201)=="switch"&&api.Keys[0x6201]==83,"old registration not restored");}
                });
                Test("disabled microphone is not registered",delegate{var api=new FakeKeys();using(var manager=new HotkeyManager(api)){manager.Apply(ToolboxSettings.Defaults());Check(manager.ActionFor(0x6204)==null&&!api.Keys.ContainsKey(0x6204),"mute enabled by default");}});
                Test("same bindings are not registered twice",delegate{var api=new FakeKeys();using(var manager=new HotkeyManager(api)){var keys=ToolboxSettings.Defaults();manager.Apply(keys);int before=api.Calls;manager.Apply(keys);Check(api.Calls==before,"unnecessary re-registration");}});
                Test("every CS2 crosshair preset is a current CS code with lossless roundtrip",delegate
                {
                    var presets=(List<CrosshairPreset>)typeof(CrosshairLibraryPage)
                        .GetMethod("Cs2Presets",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                    Check(presets.Count==8,"crosshair presets missing");
                    foreach(var preset in presets)
                    {
                        Check(Cs2ShareCode.IsCurrent(preset.Code),"invalid current code for "+preset.Name);
                        Check(Cs2ShareCode.Encode(Cs2ShareCode.Decode(preset.Code))==preset.Code,"share code does not roundtrip for "+preset.Name);
                        Check(preset.ShareCode==preset.Code&&preset.Code.IndexOf("cl_crosshair",StringComparison.OrdinalIgnoreCase)<0,
                            "obsolete console command or old share code remains");
                    }
                    Check(!Cs2ShareCode.IsCurrent("CSGO-enpB5-JqrhD-bPCTO-AyokR-LkjPK"),"old v1 code accepted");
                    Check(!Cs2ShareCode.IsCurrent("CSGO-Tbejq-X9ZF4-FppRQ-TNK4C-eamjD"),"old wrapper accepted as current export");
                    string current=presets[0].Code;
                    Check(!Cs2ShareCode.IsCurrent(current.Substring(0,45)+(current[45]=='A'?"B":"A")),"bad checksum accepted");
                    Cs2CrosshairData donk=Cs2ShareCode.Decode(current);
                    Check(donk.r==0&&donk.g==255&&donk.b==165&&donk.thickness==3&&donk.screenHeight==960,"donk parameters differ from original source");
                });
                Test("CS2 normal output never enables practice",delegate{string s=Cs2Commands.Generate(new Cs2CommandOptions());Check(s.Contains("fps_max 300")&&!s.Contains("sv_cheats")&&!s.Contains("bind \""),"unexpected command");});
                Test("practice commands require explicit option",delegate{string s=Cs2Commands.Generate(new Cs2CommandOptions {Practice=true,UnlimitedAmmo=true,GrenadePreview=true});Check(s.Contains("sv_cheats 1")&&s.Contains("sv_infinite_ammo 1")&&s.Contains("sv_grenade_trajectory_prac_trailtime 8"),"missing practice commands");});
                Test("buy commands use whitelist and unique items",delegate{string s=Cs2Commands.Generate(new Cs2CommandOptions {Items=new[]{"ak47","ak47","smokegrenade"}});Check(s.Contains("bind \"f6\" \"buy ak47; buy smokegrenade\""),"invalid buy bind");});
                Test("reject command injection through key or item",delegate{Throws(delegate{Cs2Commands.Generate(new Cs2CommandOptions {BuyKey="F6\";quit"});});Throws(delegate{Cs2Commands.Generate(new Cs2CommandOptions {Items=new[]{"ak47; quit"}});});});
                Test("game optimizer targets only recognized game executables",delegate{
                    Check(GameOptimizer.ExecutableName("CS2")=="cs2.exe"&&GameOptimizer.ExecutableName("VALORANT")=="VALORANT-Win64-Shipping.exe","wrong game target");
                    Check(GameOptimizer.IsExecutable("CS2","C:\\Steam\\cs2.exe")&&!GameOptimizer.IsExecutable("CS2","C:\\Steam\\launcher.exe"),"CS2 path validation failed");
                    Check(GameOptimizer.IsExecutable("VALORANT","C:\\Riot\\VALORANT-Win64-Shipping.exe")&&!GameOptimizer.IsExecutable("VALORANT","C:\\Riot\\RiotClientServices.exe"),"VALORANT path validation failed");
                    Throws(delegate{GameOptimizer.ExecutableName("other");});
                });
                Test("real tuning backend initializes all native constants",delegate{
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(WindowsTuningBackend).TypeHandle);
                    Check(new WindowsTuningBackend().Desired("high-power",string.Empty)=="8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c","wrong Windows high-performance scheme");
                });
                Test("expanded tuning catalog has unique real operations and explicit tradeoffs",delegate{
                    Check(SystemTuning.Definitions.Length==42,"expected 42 tuning operations");
                    Check(SystemTuning.Definitions.Select(d=>d.Id).Distinct().Count()==42,"duplicate tuning ids");
                    Check(SystemTuning.Definitions.Count(d=>d.Category=="服务")==8,"optional services missing");
                    Check(SystemTuning.Definitions.Where(d=>d.Category=="服务").All(d=>d.Risk=="功能影响"&&d.Restart),"services lack impact disclosure");
                    var options=(Dictionary<string,string[]>)typeof(WindowsTuningBackend).GetField("PowerOptions",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                    foreach(var option in options.Values){Guid guid;uint value;Check(Guid.TryParse(option[0],out guid)&&Guid.TryParse(option[1],out guid)&&uint.TryParse(option[2],out value),"invalid power definition");}
                });
                foreach(var definition in SystemTuning.Definitions.Where(d=>!d.PerGame))
                {
                    var currentDefinition=definition;
                    Test("reversible tuning with real desired value: "+currentDefinition.Id,delegate{
                        WithTuning(delegate(string file,FakeTuning api){
                            string id=currentDefinition.Id;
                            string original=id.StartsWith("service-")?"svc:2:1":currentDefinition.Category=="电源"?(id=="high-power"?"381b4222-f694-41f0-9685-ff5bb260df2e":"381b4222-f694-41f0-9685-ff5bb260df2e:37"):
                                id=="mouse-acceleration"?"6,10,1":id=="filter-shortcut"?"24,7,1,2,3,4":id=="sticky-shortcut"||id=="toggle-shortcut"?"8,7":id=="game-capture"?"d:1|d:1":id=="game-mode"||id=="gamebar-controller"||id=="transparency"||id.StartsWith("privacy-")||id=="background-apps"?"missing":"1";
                            api.NativeDesired=true;api.Values[id]=original;
                            new SystemTuning(api,file).Change(new[]{id},null,false);
                            Check(api.Values[id]==new WindowsTuningBackend().Desired(id,original),"real target was not applied");
                            new SystemTuning(api,file).Change(new[]{id},null,true);
                            Check(api.Values[id]==original,"original was not restored on reopen");
                        });
                    });
                }
                Test("optimization profiles roundtrip only whitelisted ids and game",delegate{
                    var profile=new TuningProfile {Version=1,Game="CS2",Ids=new List<string>{"game-mode","service-search","cpu-parking"}};
                    using(var stream=new MemoryStream()){new XmlSerializer(typeof(TuningProfile)).Serialize(stream,profile);stream.Position=0;var loaded=TuningProfile.Read(stream);Check(loaded.Game=="CS2"&&loaded.Ids.SequenceEqual(profile.Ids),"profile roundtrip failed");}
                    foreach(var invalid in new[]{new TuningProfile {Version=2,Game="CS2",Ids=new List<string>()},new TuningProfile {Version=1,Game="other",Ids=new List<string>()},new TuningProfile {Version=1,Game="CS2",Ids=new List<string>{"arbitrary-command"}},new TuningProfile {Version=1,Game="CS2",Ids=new List<string>{"game-mode","game-mode"}}})Throws(invalid.Validate);
                });
                Test("optional service restores automatic delayed startup without stopping service",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new ServiceTuning();var tune=new SystemTuning(api,file);tune.Change(new[]{"service-search"},null,false);Check(api.Read("service-search",null)=="svc:4:0","service target incorrect");new SystemTuning(api,file).Change(new[]{"service-search"},null,true);Check(api.Read("service-search",null)=="svc:2:1","delayed startup was lost");Check(string.Join(",",api.Calls)=="delay:False,start:4,start:2,delay:True","unexpected service operations");});
                });
                Test("partial service configuration failure rolls back delayed startup",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new ServiceTuning {FailDisable=true};new SystemTuning(api,file).Change(new[]{"service-search"},null,false);Check(api.Read("service-search",null)=="svc:2:1","partial service write not restored");using(var stream=File.OpenRead(file))Check(((TuningBackups)new XmlSerializer(typeof(TuningBackups)).Deserialize(stream)).Items.Count==0,"completed rollback retained active backup");});
                });
                Test("invalid service states reject before any native operation",delegate{
                    foreach(string value in new[]{"svc:0:0","svc:1:0","svc:4:1","svc:2:2","4","svc:999:0"})Throws(delegate{WindowsTuningBackend.WriteServiceState(value,delegate{throw new Exception("read called on invalid state");},delegate(uint start){throw new Exception("write called on invalid state");},delegate(bool delayed){throw new Exception("write called on invalid state");});});
                });
                Test("optimization profile XML rejects DTD without opening external resources",delegate{
                    string xml="<!DOCTYPE TuningProfile [<!ENTITY x SYSTEM 'file:///not-a-profile'>]><TuningProfile><Version>1</Version><Game>&x;</Game></TuningProfile>";
                    bool rejected=false;using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml))){try{TuningProfile.Read(stream);}catch(InvalidOperationException){rejected=true;}catch(System.Xml.XmlException){rejected=true;}}Check(rejected,"DTD was accepted");
                });
                if(args.Contains("--native-read"))
                {
                    var nativeTuning=new WindowsTuningBackend();
                    foreach(var definition in SystemTuning.Definitions.Where(d=>!d.PerGame))
                    {
                        var currentDefinition=definition;
                        Test("native tuning read only: "+currentDefinition.Id,delegate{
                            try
                            {
                                string original=nativeTuning.Read(currentDefinition.Id,string.Empty);
                                Check(!string.IsNullOrEmpty(original),"empty native setting");
                                Check(!string.IsNullOrEmpty(nativeTuning.Desired(currentDefinition.Id,original)),"empty desired value");
                            }
                            catch(InvalidOperationException ex)
                            {
                                bool absentPlan=currentDefinition.Id=="high-power"&&ex.Message=="系统未提供高性能计划；可使用 Windows 电源模式设置。";
                                bool absentSetting=currentDefinition.Category=="电源"&&ex.Message=="Windows 操作失败：2";
                                bool absentService=currentDefinition.Category=="服务"&&(ex.Message=="此系统未安装该可选服务。"||ex.Message=="该服务保留了延迟启动配置；请使用系统服务管理器。");
                                bool absentPolicy=(currentDefinition.Id.StartsWith("privacy-")||currentDefinition.Id=="background-apps")&&ex.Message=="当前 Windows 版本不支持此策略；请使用系统隐私设置。";
                                if(!absentPlan&&!absentSetting&&!absentService&&!absentPolicy)throw;
                                Console.WriteLine("UNAVAILABLE (documented Windows capability): "+ex.Message);
                            }
                        });
                    }
                    foreach(var definition in SystemTuning.Definitions.Where(d=>d.PerGame))
                    {
                        var currentDefinition=definition;
                        Test("native per-game tuning read only: "+currentDefinition.Id,delegate{
                            // Query only: never create the executable or any registry key/value.
                            string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cs2.exe");
                            string original=nativeTuning.Read(currentDefinition.Id,path);
                            Check(!string.IsNullOrEmpty(original)&&!string.IsNullOrEmpty(nativeTuning.Desired(currentDefinition.Id,original)),"invalid per-game read");
                        });
                    }
                    Test("Core Audio device and session enumeration (read only)",delegate
                    {
                        var render=GameAudio.Devices(0);var capture=GameAudio.Devices(1);int sessions=0;
                        foreach(var device in render)sessions+=GameAudio.Sessions(device.Id).Count;
                        Console.WriteLine("Audio outputs="+render.Count+" microphones="+capture.Count+" sessions="+sessions);
                        Check(render.Count>0,"no output devices read");
                    });
                }
                Test("retired frame recorder is not bundled",delegate{Check(Assembly.GetExecutingAssembly().GetManifestResourceStream("NexaArena.PresentMon.exe")==null,"PresentMon still bundled");});
                Test("process writes refresh cached values before verifying success",delegate{
                    int actual=0,cached=0;string order="";
                    Throws(delegate{GameOptimizer.WriteAndVerify(delegate{cached=1;order+="write ";},delegate{cached=actual;order+="refresh ";},delegate{order+="verify";return cached==1;},"silent no-op");});
                    Check(order=="write refresh verify"&&cached==0,"cached setter value was accepted as native state");
                    GameOptimizer.WriteAndVerify(delegate{actual=1;},delegate{cached=actual;},delegate{return cached==1;},"valid write rejected");
                });
                Test("system tuning never reports success for silently ignored writes",delegate{
                    WithTuning(delegate(string file,FakeTuning api){api.Values["game-mode"]="missing";api.IgnoreWrites=true;var tune=new SystemTuning(api,file);Check(!TuningSucceeded(tune.Change(new[]{"game-mode"},null,false)),"unchanged value reported success");Check(api.Values["game-mode"]=="missing","unexpected native state");});
                });
                Test("system tuning does not write or retain unsaved originals on backup failure",delegate{
                    WithTuning(delegate(string file,FakeTuning api){Directory.CreateDirectory(file+".tmp");var tune=new SystemTuning(api,file);Check(!TuningSucceeded(tune.Change(new[]{"game-mode"},null,false))&&api.Writes==0,"write proceeded without durable backup");Directory.Delete(file+".tmp");Check(TuningSucceeded(tune.Change(new[]{"game-mode"},null,false)),"failed backup remained in memory");});
                });
                Test("system tuning preserves backup after silent restore failure",delegate{
                    WithTuning(delegate(string file,FakeTuning api){var tune=new SystemTuning(api,file);tune.Change(new[]{"game-mode"},null,false);api.IgnoreWrites=true;Check(!TuningSucceeded(tune.Change(new[]{"game-mode"},null,true)),"silent restore reported success");api.IgnoreWrites=false;Check(TuningSucceeded(tune.Change(new[]{"game-mode"},null,true)),"restore original backup lost");});
                });
                Test("system tuning recovers after backup cleanup failure without rewriting original",delegate{
                    WithTuning(delegate(string file,FakeTuning api){var tune=new SystemTuning(api,file);tune.Change(new[]{"game-mode"},null,false);Directory.CreateDirectory(file+".tmp");Check(!TuningSucceeded(tune.Change(new[]{"game-mode"},null,true)),"failed backup cleanup reported success");int writes=api.Writes;Directory.Delete(file+".tmp");Check(TuningSucceeded(tune.Change(new[]{"game-mode"},null,true))&&api.Writes==writes,"original was rewritten or backup lost");});
                });
                Test("optimization originals survive reopen and missing values are restored",delegate{
                    WithTuning(delegate(string file,FakeTuning api){api.Values["game-mode"]="missing";var tune=new SystemTuning(api,file);tune.Change(new[]{"game-mode"},null,false);Check(api.Values["game-mode"]=="d:1","not applied");new SystemTuning(api,file).Change(new[]{"game-mode"},null,true);Check(api.Values["game-mode"]=="missing","missing original not restored");});
                });
                Test("optimization failed write rolls back the individual original",delegate{
                    WithTuning(delegate(string file,FakeTuning api){api.Values["game-mode"]="missing";api.Fail=true;new SystemTuning(api,file).Change(new[]{"game-mode"},null,false);Check(api.Values["game-mode"]=="missing","failed write not rolled back");});
                });
                Test("optimization manual changes are preserved and backup remains",delegate{
                    WithTuning(delegate(string file,FakeTuning api){api.Values["game-mode"]="missing";var tune=new SystemTuning(api,file);tune.Change(new[]{"game-mode"},null,false);api.Values["game-mode"]="d:8";tune.Change(new[]{"game-mode"},null,true);Check(api.Values["game-mode"]=="d:8","manual value overwritten");using(var stream=File.OpenRead(file))Check(((TuningBackups)new XmlSerializer(typeof(TuningBackups)).Deserialize(stream)).Items.Count==1,"backup discarded");});
                });
                Test("unknown optimization ids cause no write",delegate{
                    WithTuning(delegate(string file,FakeTuning api){Throws(delegate{new SystemTuning(api,file).Change(new[]{"game-mode","unknown"},null,false);});Check(api.Writes==0,"partially wrote an invalid batch");});
                });
                Test("standby cleaning requires both thresholds game gate and cooldown",delegate{
                    Check(MemoryCleaner.ShouldClean(2048,512,1024,1024,true,true,false),"pressure not detected");
                    Check(!MemoryCleaner.ShouldClean(512,512,1024,1024,true,true,false),"standby below threshold purged");
                    Check(!MemoryCleaner.ShouldClean(2048,1024,1024,1024,true,true,false),"free at boundary purged");
                    Check(!MemoryCleaner.ShouldClean(2048,512,1024,1024,true,false,false),"purged without game");
                    Check(!MemoryCleaner.ShouldClean(2048,512,1024,1024,true,true,true),"cooldown ignored");
                });
                Test("game program selection and per-game paths persist after reopening",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){string cs=Path.Combine(Path.GetDirectoryName(file),"cs2.exe"),val=Path.Combine(Path.GetDirectoryName(file),"VALORANT-Win64-Shipping.exe");File.WriteAllText(cs,"");File.WriteAllText(val,"");var paths=new GamePrograms(file,g=>null,g=>new string[0]);paths.SelectPath("CS2",cs);paths.SelectPath("VALORANT",val);var reopened=new GamePrograms(file,g=>null,g=>new string[0]);Check(reopened.LastGame=="VALORANT"&&reopened.Resolve("CS2").Path==cs&&reopened.Resolve("VALORANT").Path==val,"game or paths were lost");});
                });
                Test("game selection stores no optimization or automatic monitoring commands",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var paths=new GamePrograms(file,g=>null,g=>new string[0]);paths.SelectGame("VALORANT");Check(new GamePrograms(file,g=>null,g=>new string[0]).LastGame=="VALORANT","last game not saved");Check(!File.ReadAllText(file).Contains("HighPower"),"game selection stored unrelated changes");Throws(delegate{paths.SelectGame("unknown");});});
                });
                Test("game program discovery rejects launchers and invalid cache paths",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){string launcher=Path.Combine(Path.GetDirectoryName(file),"RiotClientServices.exe");File.WriteAllText(launcher,"");var paths=new GamePrograms(file,g=>null,g=>new[]{launcher});Throws(delegate{paths.SelectPath("VALORANT",launcher);});Check(paths.Resolve("VALORANT").Path==null&&!File.Exists(file),"launcher selected or readonly discovery wrote preferences");Check(GamePrograms.ValidPath("CS2","cs2.exe")==null,"relative program path accepted");});
                });
                Test("game program discovery uses unique installs and never guesses among multiple installs",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){string root=Path.GetDirectoryName(file),one=Path.Combine(root,"cs2.exe"),folder=Path.Combine(root,"second");Directory.CreateDirectory(folder);string two=Path.Combine(folder,"cs2.exe");File.WriteAllText(one,"");File.WriteAllText(two,"");var unique=new GamePrograms(file,g=>null,g=>new[]{one,one});Check(unique.Resolve("CS2").Path==one,"unique install not resolved");var multiple=new GamePrograms(file,g=>null,g=>new[]{one,two});var result=multiple.Resolve("CS2");Check(result.Path==null&&result.Candidates.Length==2&&!string.IsNullOrEmpty(result.Message),"multiple installs were guessed");multiple.SelectPath("CS2",two);Check(multiple.Resolve("CS2").Path==two,"explicit installation not remembered");});
                });
                Test("game program cache is revalidated and missing paths are rediscovered",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){string first=Path.Combine(Path.GetDirectoryName(file),"cs2.exe"),folder=Path.Combine(Path.GetDirectoryName(file),"moved");Directory.CreateDirectory(folder);string moved=Path.Combine(folder,"cs2.exe");File.WriteAllText(first,"");File.WriteAllText(moved,"");int reads=0;var paths=new GamePrograms(file,g=>null,g=>{reads++;return new[]{moved};});paths.SelectPath("CS2",first);File.Delete(first);Check(paths.Resolve("CS2").Path==moved&&reads==1,"stale path used");paths.Resolve("CS2");Check(reads==1,"install records scanned repeatedly");paths.Resolve("CS2",true);Check(reads==2,"refresh did not rescan");});
                });
                Test("running game path takes precedence without changing saved selection",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){string root=Path.GetDirectoryName(file),saved=Path.Combine(root,"cs2.exe"),folder=Path.Combine(root,"active");Directory.CreateDirectory(folder);string active=Path.Combine(folder,"cs2.exe");File.WriteAllText(saved,"");File.WriteAllText(active,"");var paths=new GamePrograms(file,g=>active,g=>new string[0]);paths.SelectPath("CS2",saved);Check(paths.Resolve("CS2").Path==active,"running install ignored");Check(new GamePrograms(file,g=>null,g=>new string[0]).Resolve("CS2").Path==saved,"readonly detection overwrote manual choice");});
                });
                Test("invalid game-program XML rejects external entities and preserves the file",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){string invalid="<!DOCTYPE data [<!ENTITY probe SYSTEM 'file:///C:/Windows/win.ini'>]><GameProgramPreferences><LastGame>&probe;</LastGame></GameProgramPreferences>";File.WriteAllText(file,invalid);var paths=new GamePrograms(file,g=>null,g=>new string[0]);Check(paths.LastGame=="CS2"&&paths.Warning!=null&&File.ReadAllText(file)==invalid,"unsafe preferences read or replaced");});
                });
                Test("Steam library metadata supports modern and legacy escaped paths",delegate{
                    var modern=GamePrograms.SteamLibraries("\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Steam\" } \"1\" { \"path\" \"D:\\\\SteamLibrary\" } }");Check(modern.Length==2&&modern.Contains(@"D:\SteamLibrary"),"modern Steam paths missed");Check(GamePrograms.SteamLibraries("\"1\" \"D:\\\\SteamLibrary\"").Length==1,"legacy Steam library missed");Check(GamePrograms.SteamLibraries("\"path\" \"\\\\\\\\server\\\\share\"").Length==0,"automatic discovery accessed a network share");Check(GamePrograms.ReadVdfValues("\"installdir\" \"Counter-Strike Global Offensive\"")["installdir"]=="Counter-Strike Global Offensive","Steam manifest missed");
                });
                Test("expanded NVIDIA catalog has distinct documented values and compound anisotropic control",delegate{
                    Check(NvidiaGameSettings.Settings.Length==19&&NvidiaGameSettings.Settings.Distinct().Count()==19,"NVIDIA definitions missing or duplicated");foreach(uint setting in NvidiaGameSettings.Settings){var choices=NvidiaGameSettings.Choices(setting);Check(choices.Count>0&&choices.Select(x=>x.value).Distinct().Count()==choices.Count,"invalid choice catalog");foreach(var choice in choices)NvidiaGameSettings.ValidateRawValues(choice.value,NvidiaGameSettings.RawValueCount(setting));}
                    Check(NvidiaGameSettings.Choices(0x10d2bb16).Any(x=>x.value=="explicit:1|explicit:16"),"16x did not set both override and level");Check(NvidiaGameSettings.Choices(0x107d639d).Any(x=>x.value=="explicit:2"),"Gamma enumeration invalid");Check(NvidiaGameSettings.Choices(0x10fc2d9c).Any(x=>x.value=="explicit:4"),"transparency bit value invalid");Check(NvidiaGameSettings.Effect(0x20c1221e).Contains("OpenGL")&&NvidiaGameSettings.Effect(0x00ac8497).Contains("全局"),"scope limits missing");
                });
                Test("NVIDIA compound backup parsing rejects partial and malformed values",delegate{
                    foreach(string value in new[]{"explicit:1","explicit:1|bad","explicit:-1|explicit:16","explicit:1|explicit:4294967296","explicit:1|explicit:+16"})Throws(delegate{NvidiaGameSettings.ValidateRawValues(value,2);});Check(NvidiaGameSettings.ValidateRawValues("inherited|explicit:16",2).Length==2,"mixed original inheritance not restorable");
                });
                Test("NVIDIA background limiter uses paired control-panel IDs, never idle FPS",delegate{
                    Check(NvidiaGameSettings.Settings.Contains(0x10835005u)&&!NvidiaGameSettings.Settings.Contains(0x10835016u),"idle application ID mislabeled as background limiter");
                    Check(NvidiaGameSettings.IsBackgroundLimit(0x10835005)&&NvidiaGameSettings.IsBackgroundLimit(0x10835006)&&!NvidiaGameSettings.IsBackgroundLimit(0x10835002),"compatibility routing affects foreground limiter");
                    Check(NvidiaGameSettings.RawValueCount(0x10835005)==2&&NvidiaGameSettings.RawValueCount(0x10835002)==1,"backup omitted control-panel value");
                    var choices=NvidiaGameSettings.Choices(0x10835005);Check(choices.Any(x=>x.value=="inherited|inherited"),"inheritance did not restore both parameters");
                    foreach(var choice in choices){var pair=NvidiaGameSettings.ValidateRawValues(choice.value,2);Check(pair[0]==pair[1],"actual FPS and panel display can diverge");}
                    Check(choices.Any(x=>x.value=="explicit:30|explicit:30")&&choices.Any(x=>x.value=="explicit:60|explicit:60"),"verified frame-rate options missing");
                    Throws(delegate{NvidiaGameSettings.ValidateRawValues("explicit:30",NvidiaGameSettings.RawValueCount(0x10835005));});
                });
                if(args.Contains("--native-read"))Test("installed game detection is readonly and finds only validated main programs",delegate{
                    var paths=new GamePrograms();foreach(string game in new[]{"CS2","VALORANT"}){var located=paths.Resolve(game,true);Check(located.Candidates.All(x=>GamePrograms.ValidPath(game,x)!=null),"invalid discovered main program");Console.WriteLine("Game discovery "+game+" source="+located.Source+" candidates="+located.Candidates.Length);}
                });
                Test("memory inputs persist without starting cleaner or timer requests",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){Func<MemorySnapshot> read=delegate{return new MemorySnapshot {totalMb=32768,availableMb=8192,minimumTimerMs=.5,maximumTimerMs=15.625,currentTimerMs=1,supported=true};};
                        using(var cleaner=new MemoryCleaner(file,read)){cleaner.SaveInputs(1024,2048,true,.5);var state=cleaner.Status();Check(!(bool)state.GetType().GetProperty("automatic").GetValue(state,null),"saving enabled automatic cleaning");Check((double)state.GetType().GetProperty("timerRequestedMs").GetValue(state,null)==0,"saving enabled timer");}
                        using(var reopened=new MemoryCleaner(file,read)){var state=reopened.Status();Check((long)state.GetType().GetProperty("freeThreshold").GetValue(state,null)==2048&&(double)state.GetType().GetProperty("timerTargetMs").GetValue(state,null)==.5,"memory inputs lost after reopen");Check(!(bool)state.GetType().GetProperty("automatic").GetValue(state,null),"reopen started monitoring");}
                    });
                });
                Test("memory preference persistence failure never updates live parameters",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){using(var cleaner=new MemoryCleaner(file,delegate{return new MemorySnapshot {totalMb=32768,minimumTimerMs=.5,maximumTimerMs=15.625,supported=true};})){Directory.CreateDirectory(file+".tmp");BackupFailure(delegate{cleaner.SaveInputs(1024,2048,true,1);});Check((long)cleaner.Status().GetType().GetProperty("freeThreshold").GetValue(cleaner.Status(),null)==1024,"failed save changed live threshold");}});
                });
                Test("memory input validation prevents incomplete drafts and unsafe XML",delegate{
                    var info=new MemorySnapshot {totalMb=32768,minimumTimerMs=.5,maximumTimerMs=15.625};foreach(long bad in new long[]{0,127,32769})Throws(delegate{MemoryCleaner.ValidatePreferences(new MemoryCleanerOptions {FreeMb=bad},info);});foreach(double bad in new[]{double.NaN,double.PositiveInfinity,0,.1,16})Throws(delegate{MemoryCleaner.ValidatePreferences(new MemoryCleanerOptions {TimerMs=bad},info);});
                    WithTuning(delegate(string file,FakeTuning ignored){File.WriteAllText(file,"<!DOCTYPE data [<!ENTITY probe SYSTEM 'file:///C:/Windows/win.ini'>]><MemoryCleanerOptions><FreeMb>&probe;</FreeMb></MemoryCleanerOptions>");using(var cleaner=new MemoryCleaner(file,delegate{return info;})){Check((long)cleaner.Status().GetType().GetProperty("freeThreshold").GetValue(cleaner.Status(),null)==1024,"unsafe memory preferences loaded");}});
                });
                Test("NVIDIA predefined VALORANT renderer and launcher are recognized together",delegate{
                    string path=@"D:\Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe";
                    Check(NvidiaGameSettings.AssociationBlockReason("VALORANT",path,2,NvidiaApps("valorant.exe","valorant-win64-shipping.exe"),true)==null,"verified Valorant preset blocked");
                    Check(NvidiaGameSettings.AssociationBlockReason("VALORANT",path,2,NvidiaApps(path,@"D:\Games\VALORANT\live\VALORANT.exe"),true)==null,"launcher installation path blocked");
                    Check(NvidiaGameSettings.AssociationBlockReason("VALORANT",path,2,NvidiaApps(path,@"E:\Other\VALORANT.exe"),true)!=null,"unrelated launcher install accepted");
                    Check(NvidiaGameSettings.AssociationBlockReason("VALORANT",path,2,NvidiaApps("valorant.exe","cs2.exe"),true)!=null,"cross-game profile accepted");
                });
                if(args.Contains("--nvidia-valorant-read"))Test("actual NVIDIA VALORANT main profile reads as manageable (read only)",delegate{
                    int index=Array.IndexOf(args,"--nvidia-valorant-read");if(index+1>=args.Length)throw new ArgumentException("Missing VALORANT path");using(var nvidia=new NvidiaGameSettings("VALORANT",args[index+1])){var settings=nvidia.Scan();foreach(var setting in settings.Take(8))Check(setting.supported,"VALORANT core setting remains readonly: "+setting.blocked);Console.WriteLine("NVIDIA VALORANT verified: "+settings[0].detail+"; writable="+settings.Count(x=>x.supported));}
                });
                Test("NVIDIA predefined CS2 main and alternate executable are writable",delegate{
                    var apps=NvidiaApps("cs2.exe","csgos2.exe");Check(NvidiaGameSettings.AssociationBlockReason("CS2",@"D:\Games\CS2\cs2.exe",2,apps,true)==null,"CS2 aliases were blocked");
                });
                Test("NVIDIA recognizes a single main executable for either game",delegate{
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",@"D:\Games\CS2\cs2.exe",1,NvidiaApps("CS2.EXE"),false)==null,"single CS2 executable blocked");
                    Check(NvidiaGameSettings.AssociationBlockReason("VALORANT",@"D:\Games\VAL\VALORANT-Win64-Shipping.exe",1,NvidiaApps("VALORANT-Win64-Shipping.exe"),false)==null,"single VALORANT executable blocked");
                });
                Test("NVIDIA rejects unknown and cross-game associations",delegate{
                    foreach(string other in new[]{"csgo.exe","other.exe","VALORANT-Win64-Shipping.exe","cs2.exe.bak","*cs2.exe"})
                        Check(NvidiaGameSettings.AssociationBlockReason("CS2",@"D:\Games\CS2\cs2.exe",2,NvidiaApps("cs2.exe",other),true)!=null,"foreign association accepted: "+other);
                    Check(NvidiaGameSettings.AssociationBlockReason("VALORANT",@"D:\Games\VAL\VALORANT-Win64-Shipping.exe",2,NvidiaApps("VALORANT-Win64-Shipping.exe","cs2.exe"),true)!=null,"CS2 accepted as VALORANT");
                });
                Test("NVIDIA rejects missing incomplete and duplicate association data",delegate{
                    string path=@"D:\Games\CS2\cs2.exe";
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,2,NvidiaApps("cs2.exe"),true)!=null,"partial enumeration accepted");
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,0,NvidiaApps(),true)!=null,"empty enumeration accepted");
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,129,NvidiaApps("cs2.exe"),true)!=null,"unbounded count accepted");
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,1,null,true)!=null,"unknown associations accepted");
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,2,NvidiaApps("cs2.exe","CS2.EXE"),true)!=null,"duplicate aliases accepted");
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,1,NvidiaApps("csgos2.exe"),true)!=null,"missing primary executable accepted");
                });
                Test("NVIDIA multi-app profiles require predefined game associations",delegate{
                    string path=@"D:\Games\CS2\cs2.exe";var apps=NvidiaApps("cs2.exe","csgos2.exe");
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,2,apps,false)!=null,"custom shared profile accepted");apps[1].Predefined=false;
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,2,apps,true)!=null,"custom alternate association accepted");
                });
                Test("NVIDIA rejects conditional launch and command-line associations",delegate{
                    foreach(int condition in new[]{0,1,2,3})
                    {
                        var apps=NvidiaApps("cs2.exe","csgos2.exe");if(condition==0)apps[1].Flags=2;if(condition==1)apps[1].Launcher="unknown.exe";
                        if(condition==2)apps[1].FileInFolder="marker.dll";if(condition==3)apps[1].CommandLine="cs2.exe -unknown";
                        Check(NvidiaGameSettings.AssociationBlockReason("CS2",@"D:\Games\CS2\cs2.exe",2,apps,true)!=null,"unverified condition accepted");
                    }
                });
                Test("NVIDIA permits selected install paths but not another installation",delegate{
                    string path=@"D:\Games\CS2\cs2.exe";
                    Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,2,NvidiaApps(path,@"D:\Games\CS2\csgos2.exe"),true)==null,"selected installation blocked");
                    foreach(string other in new[]{@"E:\Other\cs2.exe",@"E:\Other\csgos2.exe",@"subdir\cs2.exe","D:cs2.exe"})
                        Check(NvidiaGameSettings.AssociationBlockReason("CS2",path,2,NvidiaApps("cs2.exe",other),true)!=null,"unverified install accepted: "+other);
                });
                Test("NVIDIA global and single-app backup identities remain compatible",delegate{
                    string path=@"D:\Games\CS2\cs2.exe";
                    Check(NvidiaGameSettings.AssociationIdentity("CS2",path,"Counter-strike 2",1,NvidiaApps("cs2.exe"))==WindowsManagerBackend.Hash("CS2|"+path.ToLowerInvariant()+"|Counter-strike 2|1"),"existing game backup identity changed");
                    Check(NvidiaGameSettings.AssociationIdentity("GLOBAL",null,"Base Profile",20,NvidiaApps())==WindowsManagerBackend.Hash("GLOBAL|global|Base Profile|20"),"existing global backup identity changed");
                    Check(NvidiaGameSettings.AssociationBlockReason("GLOBAL",null,20,null,false)==null,"explicit global settings were incorrectly blocked");
                });
                Test("NVIDIA multi-app identity tracks membership not enumeration order",delegate{
                    string path=@"D:\Games\CS2\cs2.exe";var original=NvidiaApps("cs2.exe","csgos2.exe");
                    string id=NvidiaGameSettings.AssociationIdentity("CS2",path,"Counter-strike 2",2,original);
                    Check(id==NvidiaGameSettings.AssociationIdentity("CS2",path,"Counter-strike 2",2,NvidiaApps("CSGOS2.EXE","CS2.EXE")),"enumeration order or case changed identity");
                    Check(id!=NvidiaGameSettings.AssociationIdentity("CS2",path,"Counter-strike 2",2,NvidiaApps("cs2.exe","foreign.exe")),"replacement association not detected");
                    original[1].Flags=2;Check(id!=NvidiaGameSettings.AssociationIdentity("CS2",path,"Counter-strike 2",2,original),"changed launch conditions not detected");
                });
                if(args.Contains("--nvidia-cs2-read"))Test("actual NVIDIA CS2 preset has writable verified game associations (read only)",delegate{
                    int index=Array.IndexOf(args,"--nvidia-cs2-read");if(index+1>=args.Length)throw new ArgumentException("Missing CS2 path");
                    using(var nvidia=new NvidiaGameSettings("CS2",args[index+1]))
                    {
                        var settings=nvidia.Scan();Check(settings.Count==NvidiaGameSettings.Settings.Length,"incomplete NVIDIA catalog");
                        foreach(var setting in settings.Take(8)){Check(setting.supported,"NVIDIA CS2 read remains blocked: "+setting.blocked);Check(!string.IsNullOrEmpty(setting.Raw)&&!string.IsNullOrEmpty(setting.Identity),"missing restore data");}
                        foreach(var setting in settings.Skip(8))Console.WriteLine("NVIDIA capability "+setting.title+" writable="+setting.supported+" current="+setting.current+" reason="+setting.blocked);
                        Console.WriteLine("NVIDIA CS2 verified: "+settings[0].detail+"; writable="+settings.Count(x=>x.supported));
                    }
                });
                Test("manager rejects silently ignored writes and restores",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager {IgnoreWrites=true};var manager=new OptimizationManagers(api,file);Throws(delegate{manager.Change("tasks","sample","false",false);});Check(api.Value=="true"&&!manager.HasBackup("tasks","sample"),"ignored apply reported success or left wrong backup");api.IgnoreWrites=false;manager.Change("tasks","sample","false",false);api.IgnoreWrites=true;Throws(delegate{manager.Change("tasks","sample",null,true);});Check(manager.HasBackup("tasks","sample")&&api.Value=="false","failed restore discarded backup");});
                });
                Test("manager never writes or claims a backup when backup persistence fails",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){Directory.CreateDirectory(file+".tmp");var api=new FakeManager();var manager=new OptimizationManagers(api,file);BackupFailure(delegate{manager.Change("tasks","sample","false",false);});Check(api.Writes==0&&!manager.HasBackup("tasks","sample"),"unpersisted backup accepted");});
                });
                Test("manager retains original when backup cleanup fails after restore",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager();var manager=new OptimizationManagers(api,file);manager.Change("tasks","sample","false",false);Directory.CreateDirectory(file+".tmp");BackupFailure(delegate{manager.Change("tasks","sample",null,true);});Check(manager.HasBackup("tasks","sample")&&api.Value=="true","original backup lost on cleanup failure");Directory.Delete(file+".tmp");manager.Change("tasks","sample",null,true);Check(!manager.HasBackup("tasks","sample"),"backup not cleaned after successful retry");});
                });
                Test("manager does not report restore success when target identity changed",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager();var manager=new OptimizationManagers(api,file);manager.Change("tasks","sample","false",false);api.BeforeWrite=delegate{api.Identity="replacement";};Throws(delegate{manager.Change("tasks","sample",null,true);});Check(manager.HasBackup("tasks","sample"),"backup cleared despite target identity change");});
                });
                Test("APPX removal success requires the package to be absent on readback",delegate{
                    AppxManagement.VerifyRemoved("Sample_1",new AppxItem[0]);Throws(delegate{AppxManagement.VerifyRemoved("Sample_1",new[]{new AppxItem {package="sample_1"}});});Throws(delegate{AppxManagement.VerifyRemoved("Sample_1",null);});
                });
                Test("manager journal persists before write and restores after reopen",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager();var manager=new OptimizationManagers(api,file);api.BeforeWrite=delegate{Check(File.Exists(file)&&manager.HasBackup("tasks","sample"),"native write preceded durable original backup");};manager.Change("tasks","sample","false",false);api.BeforeWrite=null;Check(api.Value=="false","manager not applied");new OptimizationManagers(api,file).Change("tasks","sample",null,true);Check(api.Value=="true","manager original not restored");});
                });
                Test("manager partial write failure rolls back original",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager {Fail=true};var manager=new OptimizationManagers(api,file);Throws(delegate{manager.Change("tasks","sample","false",false);});Check(api.Value=="true"&&!manager.HasBackup("tasks","sample"),"manager rollback incomplete");});
                });
                Test("manager preserves outside changes and device-definition conflicts",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager();var manager=new OptimizationManagers(api,file);manager.Change("tasks","sample","false",false);api.Value="external";Throws(delegate{manager.Change("tasks","sample",null,true);});Check(api.Writes==1&&manager.HasBackup("tasks","sample"),"outside change overwritten");api.Value="false";api.Identity="replacement";Throws(delegate{manager.Change("tasks","sample",null,true);});Check(api.Writes==1,"replacement target overwritten");});
                });
                Test("manager rejects unrecognized choices and targets before write",delegate{
                    WithTuning(delegate(string file,FakeTuning ignored){var api=new FakeManager();var manager=new OptimizationManagers(api,file);Throws(delegate{manager.Change("tasks","sample","arbitrary",false);});Throws(delegate{manager.Change("tasks","unknown","false",false);});Check(api.Writes==0,"invalid manager request wrote setting");});
                });
                Test("process profiles validate CPU masks and never permit realtime",delegate{
                    Check(GameProcessProfiles.AffinityMask("",128)==0,"unchanged affinity was rejected");Check(GameProcessProfiles.AffinityMask("0xF",4)==15,"hex mask invalid");Check(GameProcessProfiles.AffinityMask("FFFFFFFFFFFFFFFF",64)==ulong.MaxValue,"64 CPU mask invalid");
                    foreach(string value in new[]{"0","10","garbage","-1"})Throws(delegate{GameProcessProfiles.AffinityMask(value,4);});Throws(delegate{GameProcessProfiles.AffinityMask("1",65);});Throws(delegate{new GameProcessProfile {Game="CS2",Priority="RealTime"}.Validate();});
                });
                Test("protected startup and task checks preserve security and Windows core",delegate{
                    Check(WindowsManagerBackend.Protected("Riot Vanguard vgc")&&WindowsManagerBackend.Protected("SecurityHealthSystray.exe"),"critical startup unprotected");Check(WindowsManagerBackend.TaskProtected(@"\Microsoft\Windows\Windows Defender\Scheduled Scan",""),"security task unprotected");Check(!WindowsManagerBackend.TaskProtected(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",""),"optional task unavailable");
                    string a="<Task xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'><Settings><Enabled>true</Enabled></Settings><Actions><Exec><Command>a.exe</Command></Exec></Actions></Task>";
                    Check(!WindowsManagerBackend.TaskProtected(@"\SampleUpdater",a.Replace("<Enabled>true</Enabled>","<IdleSettings><StopOnIdleEnd>true</StopOnIdleEnd></IdleSettings>")),"XML element name falsely matched security vendor");
                    Check(WindowsManagerBackend.TaskIdentity(a)==WindowsManagerBackend.TaskIdentity(a.Replace("true","false")),"enabled toggle changed task identity");Check(WindowsManagerBackend.TaskIdentity(a)!=WindowsManagerBackend.TaskIdentity(a.Replace("a.exe","b.exe")),"task replacement not detected");
                });
                Test("DNS backup distinguishes automatic from fixed IPv4 without accepting commands",delegate{
                    Check(WindowsManagerBackend.DnsValues("automatic")==null,"automatic converted into static servers");Check(WindowsManagerBackend.DnsValues("dns:223.5.5.5,223.6.6.6").Length==2,"DNS backup parse invalid");foreach(string value in new[]{"dns:1.1.1.1;quit","dns:not-an-ip","dns:::1","static:1.1.1.1"})Throws(delegate{WindowsManagerBackend.DnsValues(value);});
                });
                if(args.Contains("--native-read"))foreach(string module in new[]{"startup","tasks","network","network-advanced","power","services","ifeo","devices","device-msi","interrupt-affinity","nvidia-global"})
                {
                    string selectedModule=module;Test("real manager enumeration read only: "+selectedModule,delegate{
                        var items=new WindowsManagerBackend().Scan(selectedModule);Check(items.Select(x=>x.id).Distinct().Count()==items.Count,"duplicate manager identity");foreach(var item in items)Check(!string.IsNullOrEmpty(item.Identity)&&item.Raw!=null,"missing raw backup identity");Console.WriteLine("Manager "+selectedModule+" rows="+items.Count+" writable="+items.Count(x=>x.supported));
                    });
                }
                Test("APPX core packages and frameworks are not removable",delegate{
                    Check(AppxManagement.IsProtected(new AppxItem {name="Microsoft.WindowsStore"}),"Store was not protected");Check(AppxManagement.IsProtected(new AppxItem {name="Optional.Framework",framework=true}),"framework was not protected");Check(!AppxManagement.IsProtected(new AppxItem {name="Optional.Game",location=@"C:\Program Files\WindowsApps\Optional"}),"optional app was incorrectly marked core");
                });
                Test("system repair never accepts arbitrary executables or shell commands",delegate{
                    foreach(string name in new[]{"sfc-verify","sfc-repair","dism-scan","dism-repair","dns-flush"})Check(SystemRepairJobs.Command(name).Length==2,"missing fixed command");
                    foreach(string name in new[]{"cmd.exe","dns-flush;quit",null,"unknown"})Throws(delegate{SystemRepairJobs.Command(name);});
                    using(var jobs=new SystemRepairJobs()){Throws(delegate{jobs.Start("sfc-repair",false);});}
                });
                if(args.Contains("--native-read"))Test("APPX enumeration in actual .NET Framework host read only",delegate{var packages=AppxManagement.List();Check(packages.Select(x=>x.package).Distinct().Count()==packages.Length,"duplicate APPX identity");Console.WriteLine("APPX packages="+packages.Length+" protected="+packages.Count(x=>x.protectedPackage));});
                if(args.Contains("--native-read"))Test("memory and timer read does not activate cleaner",delegate{var memory=MemoryCleaner.Read();Check(memory.totalMb>0&&memory.availableMb>=0,"invalid memory status");Console.WriteLine("Memory total="+memory.totalMb+" MB standby="+memory.standbyMb+" MB supported="+memory.supported);});
                Console.WriteLine("Result=PASS Tests="+count+" (no user display/audio settings changed)");return 0;
            }
            catch(Exception ex){Console.WriteLine("FAIL "+ex);return 1;}
        }
        private sealed class SensitivityPageProbe:IDisposable
        {
            public readonly List<ToolboxSettings> Saved=new List<ToolboxSettings>();
            public readonly SensitivityPage Page;
            public readonly ComboBox Formula,Source,Target,SourceResolution,TargetResolution;
            public readonly NumericUpDown Sensitivity,CustomRatio,Dpi,TargetDpi;
            public readonly Label Result;
            public readonly TableLayoutPanel InputGrid;
            public readonly ModernCard InputCard;
            public readonly Control CustomLabel;
            public SensitivityPageProbe()
            {
                // No Form, Show, CreateControl, message loop, save-button click or native setting call.
                Page=new SensitivityPage(delegate{return new ToolboxSettings();},delegate(ToolboxSettings next){Saved.Add(next);});
                Formula=Field<ComboBox>("formula");Source=Field<ComboBox>("source");Target=Field<ComboBox>("target");
                SourceResolution=Field<ComboBox>("sourceResolution");TargetResolution=Field<ComboBox>("targetResolution");
                Sensitivity=Field<NumericUpDown>("sensitivity");CustomRatio=Field<NumericUpDown>("customRatio");
                Dpi=Field<NumericUpDown>("dpi");TargetDpi=Field<NumericUpDown>("targetDpi");
                Result=Field<Label>("result");InputGrid=Field<TableLayoutPanel>("inputGrid");InputCard=Field<ModernCard>("inputCard");
                // Hidden children need not be returned by GetControlFromPosition. Use their declared cell to find the label.
                CustomLabel=InputGrid.Controls.Cast<Control>().SingleOrDefault(control=>InputGrid.GetCellPosition(control).Column==0&&InputGrid.GetCellPosition(control).Row==3);
                Check(CustomLabel!=null,"custom parameter label is missing from its declared cell");
                Check(CustomLabel==Field<Label>("customRatioLabel"),"page does not retain its custom label reference");
                Page.Size=new Size(1000,900);
            }
            private T Field<T>(string name) where T:class
            {
                FieldInfo field=typeof(SensitivityPage).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
                Check(field!=null,"missing page field "+name);T value=field.GetValue(Page) as T;Check(value!=null,"invalid page field "+name);return value;
            }
            public void SelectFormula(string method)
            {
                Formula.SelectedIndex=Array.IndexOf(SensitivityFormulas.Ids,method);CheckState(method);
            }
            public void CheckState(string method)
            {
                Page.PerformLayout();InputCard.PerformLayout();InputGrid.PerformLayout();
                bool custom=method=="custom-ratio",sameGame=(string)Source.SelectedItem==(string)Target.SelectedItem;
                Check(InputGrid.RowStyles[3].SizeType==SizeType.Absolute,"custom row became a percentage filler");
                Equal(InputGrid.RowStyles[3].Height,custom?36:0);Equal(InputGrid.GetRowHeights()[3],custom?36:0);
                Check(InputCard.Height==(custom?402:366),"input card did not follow custom-row height");
                Check(CustomRatio.Visible==custom&&CustomLabel.Visible==custom,"custom controls have inconsistent visibility");
                Check(CustomRatio.Enabled==(custom&&!sameGame),"custom ratio has incorrect enabled state");
                Check(SourceResolution.Enabled==(method=="screen-center")&&TargetResolution.Enabled==(method=="screen-center"),"resolution controls have incorrect enabled state");
                double expected=SensitivityFormulas.Convert(method,(string)Source.SelectedItem,(string)Target.SelectedItem,
                    (SensitivityResolution)SourceResolution.SelectedItem,(SensitivityResolution)TargetResolution.SelectedItem,
                    (double)Sensitivity.Value,(double)Dpi.Value,(double)TargetDpi.Value,(double)CustomRatio.Value);
                Check(Result.Text==(string)Target.SelectedItem+"  "+SensitivityProjection.DisplayNumber(expected),"page result is stale or incorrect for "+method);
            }
            public void Dispose(){Page.Dispose();}
        }
        private static List<NvidiaGameSettings.ApplicationAssociation> NvidiaApps(params string[] names)
        {return names.Select(x=>new NvidiaGameSettings.ApplicationAssociation {Name=x,Predefined=true,Launcher=string.Empty,FileInFolder=string.Empty,CommandLine=string.Empty}).ToList();}
        private static bool TuningSucceeded(object result)
        {return ((System.Collections.IEnumerable)result.GetType().GetProperty("outcomes").GetValue(result,null)).Cast<object>().All(x=>(bool)x.GetType().GetProperty("ok").GetValue(x,null));}
        private static void WithTuning(Action<string,FakeTuning> test)
        {
            string folder=Path.Combine(Path.GetTempPath(),"nexa-tuning-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{test(Path.Combine(folder,"backups.xml"),new FakeTuning());}finally{Directory.Delete(folder,true);}
        }
        private sealed class FakeTuning:ITuningBackend
        {
            public readonly Dictionary<string,string> Values=new Dictionary<string,string>();
            public bool Fail,NativeDesired,IgnoreWrites;public int Writes;
            public string Read(string id,string path){string value;return Values.TryGetValue(id,out value)?value:"missing";}
            public string Desired(string id,string original){return NativeDesired?new WindowsTuningBackend().Desired(id,original):id=="game-mode"?"d:1":"d:0";}
            public void Write(string id,string path,string value){Writes++;if(IgnoreWrites)return;Values[id]=value;if(Fail){Fail=false;throw new InvalidOperationException("injected write failure");}}
        }
        private sealed class ServiceTuning:ITuningBackend
        {
            private uint start=2;private bool delayed=true;
            public bool FailDisable;
            public readonly List<string> Calls=new List<string>();
            public string Read(string id,string path){return "svc:"+start+":"+(delayed?"1":"0");}
            public string Desired(string id,string original){return new WindowsTuningBackend().Desired(id,original);}
            public void Write(string id,string path,string value)
            {WindowsTuningBackend.WriteServiceState(value,delegate{return Read(id,path);},delegate(uint next){Calls.Add("start:"+next);if(FailDisable&&next==4){FailDisable=false;throw new InvalidOperationException("injected partial service failure");}start=next;},delegate(bool next){Calls.Add("delay:"+next);delayed=next;});}
        }
        private sealed class FakeManager:IManagerBackend
        {
            public string Value="true",Identity="original";public bool Fail,IgnoreWrites;public int Writes;public Action BeforeWrite;
            public List<ManagerItem> Scan(string module){return new List<ManagerItem>{Read(module,"sample")};}
            public ManagerItem Read(string module,string id){if(module!="tasks"||id!="sample")throw new ArgumentException("unknown manager target");return new ManagerItem {id=id,title=id,Raw=Value,Identity=Identity,supported=true};}
            public string Desired(string module,string id,string choice){Read(module,id);if(choice!="false"&&choice!="true")throw new ArgumentException("unknown choice");return choice;}
            public void Write(string module,string id,string value){Read(module,id);if(BeforeWrite!=null)BeforeWrite();Writes++;if(IgnoreWrites)return;Value=value;if(Fail){Fail=false;throw new InvalidOperationException("injected partial manager failure");}}
        }
        private sealed class FakeKeys:IHotkeyBackend
        {
            public int Calls,BlockedKey;
            public readonly Dictionary<int,int> Keys=new Dictionary<int,int>();
            public bool Register(int id,uint modifiers,int key){Calls++;if(key==BlockedKey)return false;Keys[id]=key;return true;}
            public void Unregister(int id){Calls++;Keys.Remove(id);}
        }
    }
}
