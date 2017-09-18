/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */
using System;
using System.Diagnostics;
using System.Reflection;
using log4net;
using System.Threading.Tasks;
using Octgn.Communication;
using Octgn.Communication.Messages;
using Octgn.Communication.Chat;
using Octgn.Communication.Serializers;

namespace Skylabs.Lobby
{
    public class Client
    {
        private static ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Username { get; private set; }
        public string Password { get; private set; }
        public User Me { get; private set; }
        public int CurrentHostedGamePort { get; set; }
        public Guid CurrentHostedGameId { get; set; }
        public bool IsConnected => _client.IsConnected;
        public event Disconnected Disconnected {
            add {
                _client.Disconnected += value;
            }
            remove {
                _client.Disconnected -= value;
            }
        }
        public event Connected Connected {
            add {
                _client.Connected += value;
            }
            remove {
                _client.Connected -= value;
            }
        }

        private readonly ILobbyConfig _config;
        private readonly Octgn.Communication.Client _client;

        public Client(ILobbyConfig config)
        {
            _config = config;
            _client = new Octgn.Communication.Client(new TcpConnection(_config.ChatHost), new XmlSerializer());
            _client.InitializeChat();
            _client.Chat().HostedGameReady += Client_HostedGameReady;
        }

        public async Task<LoginResultType> Connect(string username, string password)
        {
            Username = username;
            Password = password;
            Me = new User(Username);
            var ret = await _client.Connect(Username, Password);

            if (ret != LoginResultType.Ok) {
                Me = null;
            }

            return ret;
        }

        public void Stop()
        {
            Log.Info("Xmpp Stop called");
            Trace.WriteLine("[Lobby]Stop Called.");
            _client.Connection.IsClosed = true;
        }

        public delegate void ClientDataRecieved(object sender, DataRecType type, object data);
        public event ClientDataRecieved OnDataReceived;

        private void Client_HostedGameReady(object sender, HostedGameReadyEventArgs e) {
            var hostedGame = e.Game;

            Log.Info($"Got hosted game {hostedGame}");
            var game = new HostedGameData(hostedGame);

            this.CurrentHostedGamePort = game.Port;
            this.CurrentHostedGameId = game.Id;
            this.OnDataReceived?.Invoke(this, DataRecType.HostedGameReady, game);
        }

        public async Task<Octgn.Communication.Chat.HostedGame> HostGame(Octgn.DataNew.Entities.Game game, string gamename,
            string password, string actualgamename, string gameIconUrl, Version sasVersion, bool specators)
        {
            var request = new HostGameRequest(game.Id, game.Version, gamename, actualgamename, gameIconUrl, password ?? "", sasVersion, specators);
            Log.Info($"{request}");

            var result = await _client.Chat().RPC.HostGame(request);

            if (result == null)
                throw new InvalidOperationException("Host game failed. No game data returned.");

            return result;

        }

        public void HostedGameStarted(string gameId)
        {
            _client.Chat().RPC.SignalGameStarted(gameId).Wait();
        }
    }

    public class InviteToGame
    {
        public User From { get; set; }
        public Guid SessionId { get; set; }
        public string Password { get; set; }
    }
}
