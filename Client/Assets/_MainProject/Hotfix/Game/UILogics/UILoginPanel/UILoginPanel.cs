/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using Cysharp.Threading.Tasks;
using FairyGUI;
using System;
using UnityEngine;

namespace PostMainland
{
    public partial class UILoginPanel : UIWrapper
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            btnLoginAsync.SetOnClickAsync(OnClickBtnLoginAsync);
        }
        private async UniTask OnClickBtnLoginAsync()
        {
            var result = await MessageBox.ShowConfirm("��������", "����");
            Log.Message(result);
            using (var loginProcess = new LoginProcess())
            {
                txtTips.text = "�������ӵ�½������...";
                await UniTask.Yield();
                var (connected,resultStr) = await loginProcess.ConnectLoginServer();
                if (connected)
                {
                    txtTips.text = "���ӵ�½�������ɹ�,���������½...";
                    await UniTask.Yield();
                    var errorCode = await loginProcess.StartLogin(inputAccount.text, inputPassword.text);
                    if (errorCode != Cfg.ErrorCode.Success)
                    {
                        txtTips.text = errorCode.ToString();
                    }
                    Log.Assert("��½�ɹ�����");
                }
                else
                {
                    txtTips.text = resultStr;
                }
            }
        }
    }
}