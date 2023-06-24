
using HybridCLR;
using Nino.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SniggerDog
{
    public class Main : MonoBehaviour
    {
        void Start()
        {
            // Editor�����£�GameCode.dll.bytes�Ѿ����Զ����أ�����Ҫ���أ��ظ����ط���������⡣
#if !UNITY_EDITOR
            // �Ȳ���Ԫ����
            LoadMetadataForAOTAssemblies();
            Assembly hotUpdateAss = Assembly.Load(File.ReadAllBytes(AppConst.GameCodeDllPath));
#else
            // Editor��������أ�ֱ�Ӳ��һ��HotUpdate����
            Assembly hotUpdateAss = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "GameCode");
#endif

            Type type = hotUpdateAss.GetType("SniggerDog.GameEntry");
            type.GetMethod("StartGame").Invoke(null, null);
        }

        private static void LoadMetadataForAOTAssemblies()
        {
            //��������dll����(4)+������+dll����(ѹ��)��
            var bytes = File.ReadAllBytes(AppConst.AotDllsPackagePath);
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }
            var reader = new Reader(bytes, bytes.Length, AppConst.AotDllCompressOption);

            var dllCount = reader.ReadInt32();
            for (int i = 0; i < dllCount; i++)
            {
                var length = reader.ReadLength();
                var dllBytes = reader.ReadBytes(length);
                var err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
                Debug.Log($"LoadMetadataForAOTAssembly:��{i}��. ret:{err}");
            }
        }
    }
}