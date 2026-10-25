using System.Globalization;
using System.Numerics;
using OmniKit.Ui;
var checks=0;
void Check(bool value,string message) { checks++; if(!value) throw new Exception(message); }
Vector2 Measure(string text,float width)
{
    var elements=StringInfo.ParseCombiningCharacters(text).Length;
    var columns=Math.Max(1,(int)(width/8));
    return new(Math.Min(elements,columns)*8,Math.Max(1,(int)Math.Ceiling((double)elements/columns))*16);
}
Check(CompanionSpeechLayout.Fit("Hello.",200,100,Measure).Text=="Hello.","short speech stays unchanged");
foreach(var text in new[]{new string('A',240),string.Concat(Enumerable.Repeat("🌙",240)),string.Concat(Enumerable.Repeat("e\u0301",240)),string.Concat(Enumerable.Repeat("👩‍🚀",80))})
foreach(var width in new[]{24f,64f,220f})
foreach(var height in new[]{16f,32f,96f})
{
    var fitted=CompanionSpeechLayout.Fit(text,width,height,Measure);
    Check(fitted.Size.X<=width && fitted.Size.Y<=height,"custom speech fits available viewport");
    var prefix=fitted.Text.EndsWith("...") ? fitted.Text[..^3] : fitted.Text;
    Check(text.StartsWith(prefix,StringComparison.Ordinal),"visible speech is a source prefix");
    Check(prefix.Length==text.Length || StringInfo.ParseCombiningCharacters(text).Contains(prefix.Length),"truncation preserves text-element boundaries");
    Check(fitted.Text==text || fitted.Text.EndsWith("..."),"truncation is visibly marked");
}
Check(CompanionSpeechLayout.Fit("Hello",10,10,Measure).Text.Length==0,"no bubble if even marker cannot fit");
Check(CompanionSpeechLayout.Fit("Hello",float.NaN,100,Measure).Text.Length==0,"invalid viewport rejected");
Check(CompanionSpeechLayout.Fit("",100,100,Measure).Text.Length==0,"empty library selection stays quiet");
Console.WriteLine($"PASS: {checks} Unicode speech-layout checks.");
