﻿using Aria2Controller.JsonRpc;
using Aria2Controller.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Aria2Controller
{
    /// <summary>
    /// 
    /// </summary>
    public enum PositionOrigin
    {
        /// <summary>
        /// 队列的开始位置
        /// </summary>
        Begin,
        /// <summary>
        /// 当前位置
        /// </summary>
        Current,
        /// <summary>
        /// 队列的结尾
        /// </summary>
        End
    }

    /// <summary>
    /// 封装调用aria2的方法
    /// </summary>
    /// <seealso cref="https://aria2.github.io/manual/en/html/aria2c.html#methods"/>
    public class Aria2Helper
    {
        const string RPC_URL = "http://localhost:6800/jsonrpc";
        const string RESULT_OK = "OK";
        const string PROTOCOL = "http";

        static Aria2Helper s_instance = new Aria2Helper();

        #region "Members"
        private string m_rpcUrl;
        private string m_host;

        /// <summary>
        /// RPC获取调用的主机名，默认使用"localhost"
        /// </summary>
        public string Host {
            get {
                return this.m_host;
            }
            set {
                if (this.m_host != value)
                {
                    this.m_host = value;
                    this.m_rpcUrl = $"{PROTOCOL}://{this.m_host}:{this.m_port}{this.m_pathName}";
                }
            }
        }
        private int m_port;

        /// <summary>
        /// RPC获取调用的端口号，默认为6800
        /// </summary>
        public int Port {
            get {
                return this.m_port;
            }
            set {
                if (this.m_port != value)
                {
                    this.m_port = value;
                    this.m_rpcUrl = $"{PROTOCOL}://{this.m_host}:{this.m_port}{this.m_pathName}";
                }
            }
        }

        /// <summary>
        /// RPC调用的路径，默认为"/jsonrpc"
        /// </summary>
        private string m_pathName;

        public string PathName {
            get {
                return this.m_pathName;
            }
            set {
                if (this.m_pathName != value)
                {
                    this.m_pathName = value;
                    this.m_rpcUrl = $"{PROTOCOL}://{this.m_host}:{this.m_port}{this.m_pathName}";
                }
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host">主机名</param>
        /// <param name="port">端口号</param>
        /// <param name="pathName">路径</param>
        public Aria2Helper(string host = "localhost", int port = 6800, string pathName = "/jsonrpc")
        {
            this.m_host = host;
            this.m_port = port;
            this.m_pathName = pathName;
            this.m_rpcUrl = $"{PROTOCOL}://{this.m_host}:{this.m_port}{this.m_pathName}";
        }

        private object RawRemoteCall(string method, object id = null, params object[] parameters)
        {
            return this.RawRemoteCall(new JsonRpcToken(method, id, parameters));
        }

        private object RawRemoteCall(JsonRpcToken token)
        {
            return JsonRpcHelper.RemoteCall(this.m_rpcUrl, token);
        }

        private T RawRemoteCall<T>(string method, object id = null, params object[] parameters)
        {
            return (T)this.RawRemoteCall(new JsonRpcToken(method, id, parameters));
        }

        private T RawRemoteCall<T>(JsonRpcToken token)
        {
            return (T)this.RawRemoteCall(token);
        }

        /// <summary>
        /// 获取aria2的是否正在运行
        /// </summary>
        public static bool IsAria2Running {
            get {
                var processes = Process.GetProcessesByName("aria2c");
                return processes.Length > 0;
            }
        }

        /// <summary>
        /// 添加下载任务，返回值为新添加下载任务的GID
        /// GID可用于控制对应任务的开始，暂停，下载进度查询，插队下载等
        /// </summary>
        public static string AddUri(string uri, IDictionary<string, string> options = null, int position = -1, object id = null)
        {
            return AddUri(new string[] { uri }, options, position, id);
        }

        /// <summary>
        /// 添加下载任务，返回值为新添加下载任务的GID
        /// GID可用于控制对应任务的开始，暂停，下载进度查询，插队下载等
        /// </summary>
        /// <param name="uris">
        /// 指向同一资源的HTTP/FTP/SFTP/BitTorrent URI数组.
        /// 当数组指的元素指向不同的资源时，添加下载任务将会失败.
        /// When adding BitTorrent Magnet URIs, uris must have only one element and it should be BitTorrent Magnet URI.
        /// </param>
        /// <param name="options">
        /// options is a struct and its members are pairs of option name and value.
        /// </param>
        /// <param name="position">
        /// If position is given, it must be an integer starting from 0.
        /// The new download will be inserted at position in the waiting queue.
        /// If position is omitted or position is larger than the current size of the queue, the new download is appended to the end of the queue.
        /// </param>
        /// <param name="id"></param>
        /// <returns>所添加任务的GID</returns>
        public static string AddUri(string[] uris, IDictionary<string, string> options = null, int position = -1, object id = null)
        {
            var token = new JsonRpcToken("aria2.addUri", id);
            if (options == null)
            {
                token.Params = new object[] { uris, new object() };
            }
            else if (position < 0)
            {
                token.Params = new object[] { uris, options };
            }
            else
            {
                token.Params = new object[] { uris, options, position };
            }
            return s_instance.RawRemoteCall<string>(token);
            //return (string)JsonRpcHelper.RemoteCall(RPC_URL, token);
        }

        /// <summary>
        /// 添加BT种子下载任务
        /// </summary>
        /// <param name="torrentPath">种子文件的路径</param>
        public static string AddTorrent(string torrentPath, object id = null)
        {
            using (var fs = File.OpenRead(torrentPath))
            {
                return AddTorrent(fs);
            }
        }

        /// <summary>
        /// 暂未测试，添加BT种子下载任务
        /// </summary>
        /// <param name="torrentStream">用于读取种子内容的流</param>
        public static string AddTorrent(Stream torrentStream, object id = null)
        {
            MemoryStream ms = new MemoryStream();
            torrentStream.CopyTo(ms);

            var torrent = Convert.ToBase64String(ms.ToArray());

            var token = new JsonRpcToken("aria2.addTorrent", id, torrent);

            return s_instance.RawRemoteCall<string>(token);
            //return (string)JsonRpcHelper.RemoteCall(RPC_URL, token);
        }

        public static void AddMetalink(string metalink)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 移除给定GID对应的下载任务
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool Remove(string gid, object id = null)
        {
            var result = s_instance.RawRemoteCall<string>("aria2.remove", id, gid);
            return result == gid;
        }

        /// <summary>
        /// 强制移除给定GID对应的下载任务
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static void ForceRemove(string gid, object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.forceRemove", id, gid);
        }

        /// <summary>
        /// 暂停给定GID对应的下载任务
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool Pause(string gid, object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.pause", id, gid);
            return (string)result == gid;
        }

        /// <summary>
        /// 暂停所有下载任务
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool PauseAll(object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.pauseAll", id);
            return (string)result == RESULT_OK;
        }

        /// <summary>
        /// 强制暂停给定GID对应的下载任务
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static void ForcePause(string gid, object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.forcePause", id, gid);
        }

        /// <summary>
        /// 强制暂停所有下载任务
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static void ForcePauseAll(object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.forcePauseAll", id);
        }

        /// <summary>
        /// 将给定GID对应的下载任务，从paused 状态变更为waiting状态,使下载重新启动
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="id"></param>
        public static bool Unpause(string gid, object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.unpause", id, gid);
            return (string)result == gid;
        }

        /// <summary>
        /// 相当于对所有下载任务调用Unpause方法
        /// This method is equal to calling aria2.unpause() for every active/waiting download. This methods 
        /// </summary>
        /// <param name="id"></param>
        public static bool UnpauseAll(object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.unpauseAll", id);
            return (string)result == RESULT_OK;
        }

        /// <summary>
        /// 获取指定GID对应任务的状态，
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="id"></param>
        /// <param name="keys">
        /// 所需要的字段，
        /// 可选参数，若不指定，则返回值将包含所有信息，指定所需要的字段，能避免不必要的数据传输
        /// 例如 指定 ["gid", "status"]，则返回Aria2TaskInfo实例仅Gid和Status属性可用，其余属性均为初始值。
        /// </param>
        /// <returns></returns>
        public static Aria2TaskInfo TellStatus(string gid, object id = null, params string[] keys)
        {
            try
            {
                var result = (JObject)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.tellStatus", id, gid, keys);
                return result.ToObject<Aria2TaskInfo>();
            }
            catch (Exception)
            {
                // TODO: 处理异常
                return new Aria2TaskInfo() { Gid = gid, Status = Models.TaskStatus.Removed };
            }
        }

        /// <summary>
        /// This method returns the URIs used in the download denoted by gid (string).
        /// The response is an array of structs and it contains following keys.
        /// </summary>
        /// <param name="gid"></param>
        public static UriToken[] GetUris(string gid, object id = null)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.getUris", id);
            return result.ToObject<UriToken[]>();
        }
        /// <summary>
        /// This method returns the file list of the download denoted by gid (string).
        /// The response is an array of structs which contain following keys.
        /// </summary>
        /// <param name="gid"></param>
        public static FileToken[] GetFiles(string gid, object id = null)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.getFiles", id);
            return result.ToObject<FileToken[]>();
        }

        /// <summary>
        /// This method returns a list peers of the download denoted by gid (string).
        /// This method is for BitTorrent only.
        /// The response is an array of structs and contains the following keys.
        /// </summary>
        public static PeerToken[] GetPeers(string gid, object id = null)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.getPeers", id);
            return result.ToObject<PeerToken[]>();
        }

        /// <summary>
        /// This method returns currently connected HTTP(S)/FTP/SFTP servers of the download denoted by gid (string).
        /// The response is an array of structs and contains the following keys. 
        /// </summary>
        public static JArray GetServers(string gid, object id = null)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.getServers", id);
            return result;
        }

        #region"Tell Methods"
        /// <summary>
        /// 获取所有正在运行的下载项目
        /// </summary>
        /// <param name="keys">参考TellStatus方法</param>
        /// <returns></returns>
        public static Aria2TaskInfo[] TellActive(object id = null, params string[] keys)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.tellActive", id, keys);

            return result.ToObject<Aria2TaskInfo[]>();
        }

        /// <summary>
        /// 异步获取所有正在运行的下载项目
        /// </summary>
        /// <param name="keys">参考TellStatus方法</param>
        /// <returns></returns>
        public static async Task<Aria2TaskInfo[]> TellActiveAsync(object id = null, params string[] keys)
        {
            var obj = JsonRpcHelper.RemoteCallAsync(RPC_URL, "aria2.tellActive", id, keys);
            var result = (JArray)(await obj);

            return result.ToObject<Aria2TaskInfo[]>();
        }

        private static Aria2TaskInfo[] _TellMethodBase(string method, int offset, int num, object id = null, params string[] keys)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, method, id, offset, num, keys);
            return result.ToObject<Aria2TaskInfo[]>();
        }

        private static async Task<Aria2TaskInfo[]> _TellMethodBaseAsync(string method, int offset, int num, object id = null, params string[] keys)
        {
            var obj = JsonRpcHelper.RemoteCallAsync(RPC_URL, method, id, offset, num, keys);
            var result = (JArray)(await obj);
            return result.ToObject<Aria2TaskInfo[]>();
        }

        /// <summary>
        /// 获取状态为waiting或者 paused的下载任务
        /// </summary>
        /// <param name="offset">
        /// offset is an integer and specifies the offset from the download waiting at the front.
        /// </param>
        /// <param name="num">num is an integer and specifies the max. number of downloads to be returned. </param>
        /// <param name="keys">参考TellStatus方法</param>
        /// <remarks>
        /// If offset is a positive integer, this method returns downloads in the range of [offset, offset + num).
        /// offset can be a negative integer.
        /// offset == -1 points last download in the waiting queue and offset == -2 points the download before the last download, and so on.
        /// Downloads in the response are in reversed order then.
        /// </remarks>
        /// <returns></returns>
        public static Aria2TaskInfo[] TellWaiting(int offset = 0, int num = 1000, object id = null, params string[] keys)
        {
            return _TellMethodBase("aria2.tellWaiting", offset, num, id, keys);
        }

        public static async Task<Aria2TaskInfo[]> TellWaitingAsync(int offset = 0, int num = 1000, object id = null, params string[] keys)
        {
            return await _TellMethodBaseAsync("aria2.tellWaiting", offset, num, id, keys);
        }

        /// <summary>
        /// This method returns a list of waiting downloads, including paused ones.
        /// offset is an integer and specifies the offset from the download waiting at the front.
        /// </summary>
        /// <param name="offset">参考TellWaiting方法</param>
        /// <param name="num"></param>
        /// </remarks>
        /// <returns></returns>
        public static Aria2TaskInfo[] TellStopped(int offset = 0, int num = 1000, object id = null, params string[] keys)
        {
            return _TellMethodBase("aria2.tellStopped", offset, num, id, keys);
        }

        public static async Task<Aria2TaskInfo[]> TellStoppedAsync(int offset = 0, int num = 1000, object id = null, params string[] keys)
        {
            return await _TellMethodBaseAsync("aria2.tellStopped", offset, num, id, keys);
        }

        #endregion

        /// <summary>
        /// 修改下载任务在队列中的位置，并返回调整后的位置。
        /// 例如：pos=-1，origin=PositionOrigin.Current时，向前移动一位
        /// pos=-1，origin=PositionOrigin.End，调整为队列中的倒数第二位
        /// </summary>
        /// <param name="gid"></param>
        /// <param name="pos"></param>
        /// <param name="origin"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static int ChangePosition(string gid, int pos, PositionOrigin origin, object id = null)
        {
            string how = "";
            switch (origin)
            {
                case PositionOrigin.Begin:
                    how = "POS_SET";
                    break;
                case PositionOrigin.Current:
                    how = "POS_CUR";
                    break;
                case PositionOrigin.End:
                    how = "POS_END";
                    break;
                default:
                    break;
            }
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.changePosition", id, gid, pos, how);
            return Convert.ToInt32(result);
        }

        public static void ChangeUri(string gid, int pos, string how, object id = null)
        {

        }

        /// <summary>
        /// This method returns options of the download denoted by gid.
        /// The response is a struct where keys are the names of options.
        /// The values are strings. 
        /// Note that this method does not return options which have no default value and have not been set on the command-line, in configuration files or RPC methods.
        /// </summary>
        public static void GetOption(string gid, object id = null)
        {

        }

        /// <summary>
        /// 清除已完成，出错，已移除的下载任务，释放内存
        /// </summary>
        public static bool PurgeDownloadResult(string gid, object id = null)
        {
            var result = JsonRpcHelper.RemoteCall(RPC_URL, "aria2.purgeDownloadResult", id);
            return (string)result == RESULT_OK;
        }

        /// <summary>
        /// 从内存中移除，给定Gid对应的已完成/出错/已移除的下载任务
        /// This method returns OK for success.
        /// </summary>
        public static bool RemoveDownloadResult(string gid, object id = null)
        {
            var result = (string)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.removeDownloadResult", id, gid);
            return result == RESULT_OK;
        }

        public static void GetVersion(string gid, object id = null)
        {
            var result = (JObject)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.getVersion", id);

        }

        public static void GetSessionInfo(object id = null)
        {
            JsonRpcHelper.RemoteCall(RPC_URL, "aria2.getSessionInfo", id);
        }

        /// <summary>
        /// 关闭Aria2
        /// </summary>
        /// <param name="id"></param>
        public static void Shutdown(object id = null)
        {
            JsonRpcHelper.RemoteCall(RPC_URL, "aria2.shutdown", id);
        }

        /// <summary>
        /// 强制关闭Aria2
        /// </summary>
        /// <param name="id"></param>
        public static void ForceShutdown(object id = null)
        {
            JsonRpcHelper.RemoteCall(RPC_URL, "aria2.forceShutdown", id);
        }

        /// <summary>
        /// 将当前的Session保存到文件中
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool SaveSession(object id = null)
        {
            return (string)JsonRpcHelper.RemoteCall(RPC_URL, "aria2.saveSession", id) == RESULT_OK;
        }
        // TODO: system.multicall

        #region "ListMethods"

        public static string[] _ListMethods(string method, object id = null)
        {
            var result = (JArray)JsonRpcHelper.RemoteCall(RPC_URL, method, id);
            return result.ToObject<string[]>();
        }

        public static async Task<string[]> _ListMethodsAsync(string method, object id = null)
        {
            var obj = JsonRpcHelper.RemoteCallAsync(RPC_URL, method, id);
            var result = (JArray)(await obj);
            return result.ToObject<string[]>();
        }


        /// <summary>
        /// This method returns all the available RPC notifications in an array of string.
        /// Unlike other methods, this method does not require secret token.
        /// This is safe because this method just returns the available notifications names.
        /// </summary>
        public static string[] ListMethods(object id = null)
        {
            return _ListMethods("system.listMethods", id);
        }

        public static async Task<string[]> ListMethodsAsync(object id = null)
        {
            return await _ListMethodsAsync("system.listMethods", id);
        }

        /// <summary>
        /// This method returns all the available RPC methods in an array of string.
        /// Unlike other methods, this method does not require secret token.
        /// This is safe because this method just returns the available method names.
        /// </summary>
        public static string[] ListNotifications(object id = null)
        {
            return _ListMethods("system.listNotifications", id);
        }

        public static async Task<string[]> ListNotificationsAsync(object id = null)
        {
            return await _ListMethodsAsync("system.listNotifications", id);
        }
        #endregion

        /// <summary>
        /// 启动Aria2，默认将会隐藏窗口
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="arg"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="hideWindow">是否隐藏窗口</param>
        public static void StartAria2(string fileName = null, string arg = "", string workingDirectory = null, bool hideWindow = true)
        {
            if (IsAria2Running)
            {
                return;
            }

            if (fileName == null)
            {
                fileName = Path.GetFullPath("aria2c.exe");
                arg = "--conf-path=aria2.conf";
            }

            if (workingDirectory == null)
            {
                workingDirectory = Path.GetDirectoryName(fileName);
            }
            Process process = Process.Start(new ProcessStartInfo(fileName, arg) { WorkingDirectory = workingDirectory });
            if (hideWindow)
            {
                IntPtr mainWindowHandle = process.MainWindowHandle;
                int num = 0;
                while (mainWindowHandle == IntPtr.Zero && num < 5)
                {
                    Thread.Sleep(200);
                    mainWindowHandle = process.MainWindowHandle;
                }
                if (mainWindowHandle != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(mainWindowHandle, WindowShowStyle.Hide);
                }
            }
        }

    }
}
