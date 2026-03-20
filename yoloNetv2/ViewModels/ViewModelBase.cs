using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace yoloNetv2.ViewModels
{
    public partial class ViewModelBase : ObservableObject
    {
        /// <summary>
        /// Tip消息
        /// </summary>
        [ObservableProperty] private bool _isShowTip = false;
        /// <summary>
        /// Tip消息
        /// </summary>
        [ObservableProperty] private string _tipMsg = "string.Empty";
        /// <summary>
        /// Tip消息字体颜色
        /// </summary>
        [ObservableProperty] private string _tipMsgColor = "#fff";
        protected async Task ShowTip(string tip, string color = "#fff")
        {
            IsShowTip = true;
            TipMsg = tip;
            TipMsgColor = color;
            await Task.Delay(3000);
            IsShowTip = false;
            TipMsg = "就绪";
        }

        protected async Task ShowTip((bool success,string msg) input)
        {
            IsShowTip = true;
            TipMsg = input.msg;
            TipMsgColor = input.success? "#03e06d": "#ff4600";
            await Task.Delay(3000);
            IsShowTip = false;
            TipMsg = "就绪";
        }
    }
}
