using System.Windows;
using Launcher.App.Services;

namespace Launcher.App;
public partial class MainWindow
{
    internal void RefreshSubscriptions()
    {
        var selected=(SubscriptionsGrid.SelectedItem as SubscriptionRow)?.Subscription.Id;
        var rows=settings.Subscriptions.Select(x=>new SubscriptionRow(x,settings.Nodes.Count(n=>n.GroupId==x.Id))).ToList();
        SubscriptionsGrid.ItemsSource=rows;
        SubscriptionsGrid.SelectedItem=rows.FirstOrDefault(x=>x.Subscription.Id==selected) ?? rows.FirstOrDefault();
        SubscriptionCountText.Text=$"{rows.Count} 个订阅 · 选中后可更新、编辑或删除";
    }
    public sealed class SubscriptionRow(Subscription subscription,int count)
    {
        public Subscription Subscription { get; }=subscription;
        public string Name=>Subscription.Name;
        public string NodesText=>$"{count} 个节点";
        public string Updated=>Subscription.UpdatedAt is { } updated ? "更新 "+updated.ToLocalTime().ToString("MM-dd HH:mm") : "尚未更新";
        public SubscriptionTraffic Traffic { get; }=subscription.Traffic ?? SubscriptionTraffic.FromLegacy(subscription.Usage) ?? new();
        public string AccessibleLabel=>$"{Name}，{NodesText}，{Traffic.UsedText}，{Traffic.RemainingText}，{Traffic.ExpiryText}，{Traffic.Status}";
    }
}
