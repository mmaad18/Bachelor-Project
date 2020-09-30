using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VaffelProgramV1
{
    class Kommunikasjon
    {
        public static MainWindow hovedVindu; // Fungerer som en pointer til hovedvinduet. Dette er nødvendig for å kunne kalle på 
                                             // funksjoner fra MainWindow. 

        //----------------------------------------| Server |-----------------------------------------

        public TcpListener lytter; // Lytter som lytter etter meldinger over TCP/IP.
        public Thread lytteTråd; // Tråd som brukes til lytting etter meldinger. 
        CultureInfo ci = new CultureInfo("en-US"); // Variabel som presiserer at tekst som sendes skal ha engelsk-Amerikansk formatering

        //----------------------------------------| Dashbord Server |-----------------------------------------

        public Thread dashConnectTråd; // 
        public Thread dashLytteTråd;

        public TcpClient dashbordKlient;
        private int port = 29999;
        private string serverIP = "192.168.1.51"; // IP'en til roboten 

        //----------------------------------------| App |-----------------------------------------

        private string serverIPmobil = "192.168.1.50"; // IP'en til mobiltelefonen 

        public TcpListener AppLytter;
        public Thread AppLytteTråd;

        public TcpClient AppKlient;

        //----------------------------------------| Stopp kommando |-----------------------------------------

        public int app_Port = 30001; // Port nr til porten som mottar oppdatering om fremskritt fra roboten 

        //-----------------------------------------------------------------------------------------------

        /// <summary>
        /// 
        /// </summary>
        public void TCPlytter()
        {
            try
            {
                while (true)
                {
                    Console.WriteLine("Kjører lytting...");

                    TcpClient nyKlient = lytter.AcceptTcpClient();
                    // Mottar klient ID 
                    string nyKlientID = null;
                    byte[] bits = new byte[1024];
                    NetworkStream stream;
                    stream = nyKlient.GetStream();
                    byte[] utBytes = new byte[1024];

                    nyKlientID = Encoding.UTF8.GetString(bits, 0, stream.Read(bits, 0, bits.Length));

                    if (nyKlientID == "@")
                    {
                        StartRobotFunksjon();
                    }
                    else if (nyKlientID == "#")
                    {
                        StoppRobotFunksjon();
                    }
                    else
                    {
                        Console.WriteLine("Koblet til PC {0} på adresse. {1}", nyKlientID, nyKlient.Client.RemoteEndPoint);

                        Thread nyKlientTråd = new Thread(EnKlient);
                        nyKlientTråd.Start(nyKlient);

                        // Sender en velkomstmelding til en klient som kobler seg opp mot sentralen.
                        utBytes = Encoding.UTF8.GetBytes("$Du er nå tilkoblet PC'n.");
                        stream.Write(utBytes, 0, utBytes.Length);
                    }

                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        private void EnKlient(object tcpClient) // Funksjon som lytter etter meldinger fra hver gitte klient.
        {
            var client = tcpClient as TcpClient;

            if (client != null)
            {
                client.ReceiveTimeout = 600000; // Hvis klient blir markert som tilkoblet, så vil sentralen vente i 600'000 ms før den dropper tilkoblingen.
            }

            try
            {
                while (true)
                {
                    string melding = null;
                    byte[] innbytes = new byte[1024];
                    byte[] utBytes = new byte[1024];
                    if (client != null)
                    {
                        var stream = client.GetStream();

                        // Mottar melding fra klient
                        melding = Encoding.UTF8.GetString(innbytes, 0, stream.Read(innbytes, 0, innbytes.Length));

                        Console.WriteLine("Melding fra {0}:\t{1}", client.Client.RemoteEndPoint, melding);

                        Thread.Sleep(10);
                        // Sender respons til klient
                        utBytes = Encoding.UTF8.GetBytes(hovedVindu.ReturFunksjon(melding));
                        stream.Write(utBytes, 0, utBytes.Length);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        //-------------------------------------------| Ethernet - Dashboard Server |--------------------------------

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="melding"></param>
        public void TcpSendMelding(object tcpClient, string melding)
        {
            string Melding = melding;
            var dashbordKlient = tcpClient as TcpClient;
            var stream = dashbordKlient.GetStream();
            byte[] bytes = Encoding.UTF8.GetBytes(Melding);
            stream.Write(bytes, 0, bytes.Length);

            Console.WriteLine(Melding);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        private void ServerLytter(object tcpClient)
        {
            var dashbordKlient = tcpClient as TcpClient;
            if (dashbordKlient != null)
            {
                dashbordKlient.ReceiveTimeout = 600000; // Hvis klient blir markert som tilkoblet, så vil sentralen vente i 600'000 ms før den dropper tilkoblingen.
            }

            try
            {
                while (true)
                {
                    string melding = null;
                    byte[] innbytes = new byte[1024];
                    byte[] utBytes = new byte[1024];
                    var stream = dashbordKlient?.GetStream();

                    // Mottar melding fra klient
                    melding = Encoding.UTF8.GetString(innbytes, 0, stream.Read(innbytes, 0, innbytes.Length));
                    Console.WriteLine(melding);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now + "\t" + ex.Message);
                MessageBox.Show(DateTime.Now + "\t" + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <returns></returns>
        private string TcpMottaMelding(object tcpClient)
        {
            string melding;
            var dashbordKlient = tcpClient as TcpClient;
            var stream = dashbordKlient.GetStream();
            byte[] bytes = new byte[1024];
            stream = dashbordKlient.GetStream();
            melding = Encoding.UTF8.GetString(bytes, 0, stream.Read(bytes, 0, bytes.Length));
            return melding;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Connect()
        {
            try
            {
                var melding = string.Format(ci, "connecting\n");
                dashbordKlient = new TcpClient(serverIP, port);

                if (dashbordKlient == null)
                {
                    hovedVindu.btnDashConn.IsEnabled = true;
                    hovedVindu.btnDashDisc.IsEnabled = false;

                    hovedVindu.btnPlayRobot.IsEnabled = true;
                    hovedVindu.btnStopRobot.IsEnabled = false;
                    return;
                }

                if (dashbordKlient.Connected)
                {
                    TcpSendMelding(dashbordKlient, melding);
                }

                // Mottar velkomstmelding fra server
                var velkommen = TcpMottaMelding(dashbordKlient);
                Console.WriteLine(velkommen);
                dashLytteTråd = new Thread(ServerLytter);
                dashLytteTråd.Start(dashbordKlient);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        //------------------------------------------| Feedback til mobil App |------------------------------------------------------
        
        /// <summary>
        /// 
        /// </summary>
        public void AppServerLytter()
        {
            try
            {
                while (true)
                {
                    Console.WriteLine("Lytter etter progress...");

                    AppKlient = AppLytter.AcceptTcpClient();

                    string melding = null;
                    byte[] innbytes = new byte[1024];
                    byte[] utBytes = new byte[1024];
                    NetworkStream stream = AppKlient.GetStream();

                    // Mottar melding fra klient
                    melding = Encoding.UTF8.GetString(innbytes, 0, stream.Read(innbytes, 0, innbytes.Length));
                    Console.WriteLine(melding);

                    Thread.Sleep(10);

                    Thread nyAppTråd = new Thread(EnAppKlient);
                    nyAppTråd.Start(AppKlient);

                    Thread.Sleep(10);

                    utBytes = Encoding.UTF8.GetBytes("$Du er nå tilkoblet PC'n.");
                    stream.Write(utBytes, 0, utBytes.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now + "\t" + ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        private void EnAppKlient(object tcpClient) // Funksjon som lytter etter meldinger fra hver gitte klient.
        {
            var client = tcpClient as TcpClient;

            if (client != null)
            {
                client.ReceiveTimeout = 600000; // Hvis klient blir markert som tilkoblet, så vil sentralen vente i 600'000 ms før den dropper tilkoblingen.
            }

            try
            {
                while (true)
                {
                    string melding = null;
                    byte[] innbytes = new byte[1024];
                    byte[] utBytes = new byte[1024];
                    if (client != null)
                    {
                        var stream = client.GetStream();

                        // Mottar melding fra klient
                        melding = Encoding.UTF8.GetString(innbytes, 0, stream.Read(innbytes, 0, innbytes.Length));

                        Console.WriteLine("Melding fra {0}:\t{1}", client.Client.RemoteEndPoint, melding);

                        Thread.Sleep(10);

                        // Sender respons til mobil App
                        AppRespons.AppConnect(serverIPmobil, 1754, melding);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void StartRobotFunksjon()
        {
            if (dashbordKlient == null)
            {
                return;
            }

            if (dashbordKlient.Connected)
            {
                TcpSendMelding(dashbordKlient, string.Format(ci, "play\n"));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void StoppRobotFunksjon()
        {
            if (AppKlient == null)
            {
                return;
            }

            if (AppKlient.Connected)
            {
                TcpSendMelding(dashbordKlient, string.Format(ci, "pause\n"));
            }
        }
        
    }
}
