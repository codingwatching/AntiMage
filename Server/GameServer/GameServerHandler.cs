﻿
using Cysharp.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using System.Text;

namespace PostMainland
{
    public partial class GameServerHandler
    {
        private IChannelHandlerContext _context;

        Dictionary<long, UniTaskCompletionSource<IResponse>> _waittingRequests = new Dictionary<long, UniTaskCompletionSource<IResponse>>();
        private void Reply(byte[] resp)
        {
            SendAsync(resp).Forget();
        }
        private void ResponseCallback(long msgId, IResponse response)
        {
            if (_waittingRequests.TryGetValue(msgId, out var source))
            {
                source.TrySetResult(response);
            }
        }
        public void SendMessage(IMessage message)
        {
            byte[] buffer = ProtocalHelper.SerializeProtocal(message, 0);
            SendAsync(buffer).Forget();
        }
        private async UniTask SendAsync(byte[] bytes)
        {
            await _context.WriteAsync(Unpooled.CopiedBuffer(bytes));
        }
        public async UniTask<TRes> RequestAsync<TRes>(IRequest request) where TRes : class, IResponse
        {
            //TODO 考虑多进程通讯
            await UniTask.CompletedTask;
            return null;
        }

    }
    public partial class GameServerHandler : ChannelHandlerAdapter
    {
        /*
        * Channel的生命周期
        * 1.ChannelRegistered 先注册
        * 2.ChannelActive 再被激活
        * 3.ChannelRead 客户端与服务端建立连接之后的会话（数据交互）
        * 4.ChannelReadComplete 读取客户端发送的消息完成之后
        * error. ExceptionCaught 如果在会话过程当中出现dotnetty框架内部异常都会通过Caught方法返回给开发者
        * 5.ChannelInactive 使当前频道处于未激活状态
        * 6.ChannelUnregistered 取消注册
        */

        /// <summary>
        /// 频道注册
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
            _context = context;
        }

        /// <summary>
        /// socket client 连接到服务端的时候channel被激活的回调函数
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelActive(IChannelHandlerContext context)
        {
            //一般可用来记录连接对象信息
            base.ChannelActive(context);
        }

        /// <summary>
        /// socket接收消息方法具体的实现
        /// </summary>
        /// <param name="context">当前频道的句柄，可使用发送和接收方法</param>
        /// <param name="message">接收到的客户端发送的内容</param>
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                ProtocalHelper.Handle(buffer.GetIoBuffer().ToArray(), Reply, ResponseCallback);
            }
        }

        /// <summary>
        /// 该次会话读取完成后回调函数
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();//将WriteAsync写入的数据流缓存发送出去

        /// <summary>
        /// 异常捕获
        /// </summary>
        /// <param name="context"></param>
        /// <param name="exception"></param>
        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            context.CloseAsync();
        }

        /// <summary>
        /// 当前频道未激活状态
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
        }

        /// <summary>
        /// 取消注册当前频道，可理解为销毁当前频道
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelUnregistered(IChannelHandlerContext context)
        {
            base.ChannelUnregistered(context);
        }
    }
}