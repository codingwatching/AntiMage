/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using Cysharp.Threading.Tasks;
using FairyGUI;
using System.Threading;

namespace PostMainland
{
    [FGUILocalLoad]
    public partial class UIResUpdatePanel : UIWrapper
    {
        private EPatchStates _states;
        protected override void OnCreate()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            PatchUpdater.InitCallback((onStateChanged) =>
            {
                _states = onStateChanged.CurrentStates;
                txtTask.text = onStateChanged.CurrentStates.ToString();

                if (onStateChanged.CurrentStates == EPatchStates.PatchDone)
                {
                    txtTask.text = "��Դ������ϣ����ڼ��غ����߼�������";
                }
            }, (onProcessUpdated) =>
            {
                string currentSizeMB = (onProcessUpdated.CurrentDownloadSizeBytes / 1048576f).ToString("f1");
                string totalSizeMB = (onProcessUpdated.TotalDownloadSizeBytes / 1048576f).ToString("f1");
                string text =
                    $"��Դ�����У�{onProcessUpdated.CurrentDownloadCount}/{onProcessUpdated.TotalDownloadCount} {currentSizeMB}MB/{totalSizeMB}MB";

                txtTask.text = text;
                progressBar.value = onProcessUpdated.CurrentDownloadSizeBytes * 1.0f /
                    onProcessUpdated.TotalDownloadSizeBytes * 100;
            });
        }
    }
}