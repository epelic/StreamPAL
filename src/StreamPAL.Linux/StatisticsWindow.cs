using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace StreamPAL.Linux;

public sealed class StatisticsWindow : Window
{
    private readonly SourceInstance _instance;private readonly TextBlock _current=new(),_maximum=new(),_updated=new();private readonly ListenerChart _chart=new(){Height=360};private readonly DispatcherTimer _timer=new(){Interval=TimeSpan.FromSeconds(2)};
    public StatisticsWindow(SourceInstance instance){_instance=instance;Title=$"Statistiche · {instance.Name}";Width=900;Height=560;MinWidth=650;var root=new StackPanel{Margin=new Thickness(18),Spacing=12};root.Children.Add(new TextBlock{Text=$"Statistiche ascoltatori · {instance.Name}",FontSize=22,FontWeight=FontWeight.Bold});root.Children.Add(Row(Card("Ascoltatori ora",_current),Card("Massimo 72 ore",_maximum),Card("Aggiornamento",_updated)));root.Children.Add(_chart);root.Children.Add(Row(Button("Esporta CSV",Export),Button("Chiudi",Close)));Content=root;_timer.Tick+=(_,_)=>Refresh();Opened+=(_,_)=>{Refresh();_timer.Start();};Closed+=(_,_)=>_timer.Stop();}
    private void Refresh(){var data=StatisticsService.Instance.Get(_instance);_current.Text=_instance.TotalListeners.ToString("N0");_maximum.Text=data.Select(x=>x.Total).DefaultIfEmpty().Max().ToString("N0");_updated.Text=data.Count==0?"In attesa":data[^1].TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm:ss");_chart.Instance=_instance;_chart.Samples=data;_chart.InvalidateVisual();}
    private async void Export(){var file=await StorageProvider.SaveFilePickerAsync(new(){SuggestedFileName=$"StreamPAL-{_instance.Name}-72h.csv",FileTypeChoices=[new("CSV"){Patterns=["*.csv"]}]});if(file is not null)StatisticsService.Instance.ExportCsv(file.Path.LocalPath,_instance);}
    private static Border Card(string label,TextBlock value){var p=new StackPanel{Margin=new Thickness(16)};p.Children.Add(new TextBlock{Text=label,Foreground=Brush.Parse("#5E7182")});value.FontSize=24;value.FontWeight=FontWeight.Bold;p.Children.Add(value);return new Border{Background=Brushes.White,CornerRadius=new CornerRadius(8),Child=p,MinWidth=190};}
    private static StackPanel Row(params Control[] items){var p=new StackPanel{Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=10};foreach(var x in items)p.Children.Add(x);return p;}
    private static Button Button(string text,Action action)=>new(){Content=text,Command=new ActionCommand(action)};
}

public sealed class ListenerChart : Control
{
    private static readonly IBrush[] Colors=[Brush.Parse("#31D2A8"),Brush.Parse("#3EA6FF"),Brush.Parse("#EE53FF"),Brush.Parse("#FFB930")];public SourceInstance? Instance{get;set;}public IReadOnlyList<ListenerSample> Samples{get;set;}=[];
    public override void Render(DrawingContext c){base.Render(c);var r=Bounds;if(r.Width<100||r.Height<100)return;var plot=new Rect(52,20,r.Width-70,r.Height-58);c.DrawRectangle(Brushes.White,new Pen(Brush.Parse("#BECBD5")),r);var max=Math.Max(10,Samples.SelectMany(x=>x.Streams.Values.Append(x.Total)).DefaultIfEmpty().Max());max=(int)Math.Ceiling(max/10d)*10;for(var i=0;i<=4;i++){var y=plot.Bottom-plot.Height*i/4;c.DrawLine(new Pen(Brush.Parse("#DCE4EA")),new Point(plot.Left,y),new Point(plot.Right,y));DrawText(c,(max*i/4).ToString(),new Point(8,y-7));}if(Instance is null)return;var start=DateTime.UtcNow.AddHours(-72);var active=Instance.Encoders.Where(e=>Samples.Any(s=>s.Streams.ContainsKey(e.Id))).ToList();for(var i=0;i<active.Count;i++)DrawSeries(c,plot,start,max,Samples.Select(s=>(s.TimestampUtc,s.Streams.GetValueOrDefault(active[i].Id))),Colors[i%Colors.Length],1.7);DrawSeries(c,plot,start,max,Samples.Select(s=>(s.TimestampUtc,s.Total)),Brushes.Black,2.5);}
    private static void DrawSeries(DrawingContext c,Rect plot,DateTime start,int max,IEnumerable<(DateTime Time,int Value)> values,IBrush brush,double width){var points=values.Where(x=>x.Time>=start).OrderBy(x=>x.Time).Select(x=>new Point(plot.Left+(x.Time-start).TotalHours/72*plot.Width,plot.Bottom-Math.Clamp(x.Value/(double)max,0,1)*plot.Height)).ToList();for(var i=1;i<points.Count;i++)c.DrawLine(new Pen(brush,width),points[i-1],points[i]);}
    private static void DrawText(DrawingContext c,string text,Point p)=>c.DrawText(new FormattedText(text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Sans"),10,Brush.Parse("#667788")),p);
}
