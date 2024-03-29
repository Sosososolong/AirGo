﻿using Sylas.RemoteTasks.Utils.CommandExecutor;

namespace Sylas.RemoteTasks.App.RemoteHostModule
{
    public class RemoteHost
    {
        public RemoteHost()
        {
            Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} created RemoteHost");
        }
        private string _name = string.Empty;
        public string Name { get => string.IsNullOrWhiteSpace(_name) ? Ip : _name; set => _name = value; }
        public string Ip { get; set; } = "";
        public int Port { get; set; } = 22;
        public string User { get; set; } = "root";
        public string PrivateKey { get; set; } = "";
        public string Pwd { get; set; } = "123456";

        public List<CommandInfo> Commands { get; set; } = [];
        public virtual List<CommandInfo> HostInfoCommands { get; set; } = [];

        private SshHelper? _sshConnection;
        public SshHelper SshConnection
        {
            get
            {
                _sshConnection ??= new(Ip, Port, User, PrivateKey);
                return _sshConnection;
            }
        }
    }
}
