using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestKlient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private Thread tcpTilkoblingstråd;
        private Thread tcpLytteTråd;
        private TcpClient Klient;
        //string serverIP = "192.168.1.50";
        string serverIP = "127.0.0.1";

        private Thread autotråd;
        string loopMelding = "";
        int Delay = 0;
        int Antall = 0;


        public MainWindow()
        {
            InitializeComponent();
            btnKobleFra.IsEnabled = false;
        }

        private void SendMeldinger(object tcpKlient)
        {
            string melding = loopMelding;
            var Klient = tcpKlient as TcpClient;
            var stream = Klient.GetStream();
            byte[] bytes = Encoding.UTF8.GetBytes(melding);

            Console.WriteLine("Teller = 0/" + Antall);

            for (int i = 1; i <= Antall; i++)
            {
                stream.Write(bytes, 0, bytes.Length);
                Console.WriteLine("Teller = " + i + "/" + Convert.ToString(Antall));
                Thread.Sleep(Delay);
            }
        }

        private void SendEnMelding(object tcpKlient, string melding)
        {
            var Klient = tcpKlient as TcpClient;
            var stream = Klient.GetStream();
            byte[] bytes = Encoding.UTF8.GetBytes(melding);
            stream.Write(bytes, 0, bytes.Length);
        }

        private void KobleTil()
        {
            try
            {
                var melding = "testklient";
                Klient = new TcpClient(serverIP, portNr());

                if(Klient == null)
                {
                    return;
                }

                if(Klient.Connected)
                {
                    SendEnMelding(Klient, melding);
                }

                var Tilbakemelding = MottaSvar(Klient);
                Console.WriteLine(Tilbakemelding);

                tcpLytteTråd = new Thread(LytterFraServer);
                tcpLytteTråd.Start(Klient);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                throw;
            }
        }

        private string MottaSvar(object tcpKlient)
        {
            string melding;
            var Klient = tcpKlient as TcpClient;
            var stream = Klient.GetStream();
            byte[] bytes = new byte[1024];
            stream = Klient.GetStream();
            melding = Encoding.UTF8.GetString(bytes, 0, stream.Read(bytes, 0, bytes.Length));
            return melding;
        }

        private void LytterFraServer(object tcpKlient)
        {
            var Klient = tcpKlient as TcpClient;

            if(Klient != null)
            {
                Klient.ReceiveTimeout = 600000; // Blir dropppet etter 10 minutter
            }

            try
            {
                while(true)
                {
                    string melding = null;
                    byte[] innbytes = new byte[1024];
                    byte[] utBytes = new byte[1024];

                    var stream = Klient?.GetStream();

                    //Mottar melding fra klient
                    melding = Encoding.UTF8.GetString(innbytes, 0, stream.Read(innbytes, 0, innbytes.Length));
                    Console.WriteLine(melding);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(DateTime.Now + "\t" + ex.Message);
            }
        }

        private void btnKobleTil_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                tcpTilkoblingstråd = new Thread(KobleTil);
                tcpTilkoblingstråd.Start();

                btnKobleTil.IsEnabled = false;
                btnKobleFra.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void btnKobleFra_Click(object sender, RoutedEventArgs e)
        {
            if(Klient == null)
            {
                return;
            }

            if(Klient.Connected)
            {
                Klient.Close();
            }

            btnKobleTil.IsEnabled = true;
            btnKobleFra.IsEnabled = false;
        }

        private void btnStartTest_Click(object sender, RoutedEventArgs e)
        {
            if (Meldingtxt.Text.Length <= 0)
            {
                return;
            }

            if(Klient == null)
            {
                return;
            }

            if(Klient.Connected)
            {
                loopMelding = Meldingtxt.Text;
                Delay = Convert.ToInt32(Delaytxt.Text);
                Antall = Convert.ToInt32(Antalltxt.Text);

                autotråd = new Thread(new ParameterizedThreadStart(SendMeldinger));
                autotråd.Start(Klient);
            }
        }

        private int portNr()
        {
            int PortNr = 30000;

            this.Dispatcher.Invoke(() =>
            {
                Int32.TryParse(PortTxt.Text, out PortNr);
            });

            return PortNr;
        }

    }
}
