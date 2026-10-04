using System;

namespace NexaArena
{
    internal sealed class Cs2CrosshairData
    {
        public int screenHeight,style,r,g,b,a,outlineR,outlineG,outlineB,outlineA;
        public int thickness,outlineMode,gap,length,dynamicSpreadLimit,splitDistance;
        public int innerSplitAlpha,outerSplitAlpha,splitSizeRatio,scopeDotScale;
        public bool followRecoil,centerDot,tStyle,scopeDotUseColor;
    }

    // Current CS2 exports (2026-09-30, depot 2347771 manifest 4472721471834965775):
    // 32 bytes, additive checksum, payload version 1, little-endian fields inside
    // a big-endian base-57 number. The visible wrapper is CS + 44 characters.
    internal static class Cs2ShareCode
    {
        private const string Alphabet="ABCDEFGHJKLMNOPQRSTUVWXYZabcdefhijkmnopqrstuvwxyz23456789";

        public static bool IsCurrent(string code)
        {
            try { Decode(code);return true; }
            catch(ArgumentException) {return false;}
        }

        public static Cs2CrosshairData Decode(string code)
        {
            if(code==null)throw new ArgumentException("准星分享码为空。");
            code=code.Trim();
            if(code.Length!=46 || !code.StartsWith("CS",StringComparison.Ordinal))
                throw new ArgumentException("需要当前 CS2 的 CS 开头、46 位分享码。");
            byte[] payload=new byte[32];
            for(int i=code.Length-1;i>=2;i--)
            {
                int carry=Alphabet.IndexOf(code[i]);
                if(carry<0)throw new ArgumentException("分享码包含无效字符。");
                for(int j=31;j>=0;j--)
                {
                    int value=payload[j]*57+carry;
                    payload[j]=(byte)value;carry=value>>8;
                }
                if(carry!=0)throw new ArgumentException("分享码超出载荷长度。");
            }
            int sum=0;for(int i=1;i<32;i++)sum+=payload[i];
            if(payload[0]!=(byte)sum || payload[1]!=1)throw new ArgumentException("分享码校验失败或格式版本不支持。");
            for(int i=23;i<32;i++)if(payload[i]!=0)throw new ArgumentException("分享码含尚未支持的扩展字段。");
            uint split=(uint)(payload[18]|payload[19]<<8|payload[20]<<16|payload[21]<<24);
            if((split&0xe0000000)!=0)throw new ArgumentException("分享码含未知分组标记。");
            var valueData=new Cs2CrosshairData {
                screenHeight=payload[2]|payload[3]<<8,style=payload[4]&31,
                followRecoil=(payload[4]&32)!=0,centerDot=(payload[4]&64)!=0,tStyle=(payload[4]&128)!=0,
                r=payload[5],g=payload[6],b=payload[7],a=payload[8],
                outlineR=payload[9],outlineG=payload[10],outlineB=payload[11],outlineA=payload[12],
                thickness=payload[13]&63,outlineMode=payload[13]>>6,
                gap=(short)(payload[14]|payload[15]<<8),length=payload[16],dynamicSpreadLimit=payload[17],
                splitDistance=(int)(split&127),innerSplitAlpha=(int)((split>>7)&127),
                outerSplitAlpha=(int)((split>>14)&127),splitSizeRatio=(int)((split>>21)&127),
                scopeDotUseColor=(split&0x10000000)!=0,scopeDotScale=payload[22]
            };
            Validate(valueData);return valueData;
        }

        public static string Encode(Cs2CrosshairData data)
        {
            Validate(data);
            byte[] p=new byte[32];p[1]=1;
            p[2]=(byte)data.screenHeight;p[3]=(byte)(data.screenHeight>>8);
            p[4]=(byte)(data.style|(data.followRecoil?32:0)|(data.centerDot?64:0)|(data.tStyle?128:0));
            p[5]=(byte)data.r;p[6]=(byte)data.g;p[7]=(byte)data.b;p[8]=(byte)data.a;
            p[9]=(byte)data.outlineR;p[10]=(byte)data.outlineG;p[11]=(byte)data.outlineB;p[12]=(byte)data.outlineA;
            p[13]=(byte)(data.thickness|(data.outlineMode<<6));
            p[14]=(byte)data.gap;p[15]=(byte)(data.gap>>8);p[16]=(byte)data.length;p[17]=(byte)data.dynamicSpreadLimit;
            uint split=(uint)(data.splitDistance|(data.innerSplitAlpha<<7)|(data.outerSplitAlpha<<14)|(data.splitSizeRatio<<21))|
                (data.scopeDotUseColor?0x10000000u:0u);
            for(int i=0;i<4;i++)p[18+i]=(byte)(split>>(i*8));
            p[22]=(byte)data.scopeDotScale;
            int sum=0;for(int i=1;i<32;i++)sum+=p[i];p[0]=(byte)sum;
            char[] text=new char[46];text[0]='C';text[1]='S';
            for(int i=2;i<46;i++)
            {
                int remainder=0;
                for(int j=0;j<32;j++){int n=(remainder<<8)|p[j];p[j]=(byte)(n/57);remainder=n%57;}
                text[i]=Alphabet[remainder];
            }
            return new string(text);
        }

        private static void Range(int value,int min,int max,string name)
        {if(value<min||value>max)throw new ArgumentException("准星字段超出范围："+name);}

        private static void Validate(Cs2CrosshairData d)
        {
            if(d==null)throw new ArgumentException("准星数据为空。");
            Range(d.screenHeight,240,65535,"screenHeight");Range(d.style,0,9,"style");
            Range(d.r,0,255,"red");Range(d.g,0,255,"green");Range(d.b,0,255,"blue");Range(d.a,0,255,"alpha");
            Range(d.outlineR,0,255,"outlineRed");Range(d.outlineG,0,255,"outlineGreen");Range(d.outlineB,0,255,"outlineBlue");Range(d.outlineA,0,255,"outlineAlpha");
            Range(d.thickness,0,32,"thickness");Range(d.outlineMode,0,2,"outlineMode");
            Range(d.gap,-3840,3840,"gap");Range(d.length,0,255,"length");Range(d.dynamicSpreadLimit,0,255,"spreadLimit");
            Range(d.splitDistance,0,127,"splitDistance");Range(d.innerSplitAlpha,0,100,"innerAlpha");
            Range(d.outerSplitAlpha,0,70,"outerAlpha");Range(d.splitSizeRatio,0,100,"splitRatio");Range(d.scopeDotScale,0,190,"scopeDotScale");
        }
    }
}
