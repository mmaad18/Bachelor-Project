using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace VaffelProgramV1
{
    class AppRespons
    {
        /// <summary>
        /// Klasse som inneholder alt som skal til for å prøve å koble seg til mobil App'en sin "server", sende
        /// en melding, og så koble ifra. 
        /// </summary>
        /// <param name="serverIP"> Dette er IP'en til mobil App'en sin server </param>
        /// <param name="portMobil"> Dette er Port Nr. til mobil App'en sin server </param>
        /// <param name="message"> Dette er meldingen som skal sendes til mobil App'en </param>
        public static void AppConnect(string serverIP, int portMobil, string message)
        {
            try 
            {
                // Kobler seg til "serveren" til mobiltelefonen. Denne får navnet AppHost. 
                TcpClient AppHost = new TcpClient(serverIP, portMobil);
                
                // Oversetter meldingen til ASCII og lagrer den i en Byte array. 
                byte[] data = Encoding.UTF8.GetBytes(message);

                // Oppretter en AppHost strøm for å lese og skrive til AppHost.
                NetworkStream stream = AppHost.GetStream();
                
                // Sender meldingen til den tilkoblede TcpServeren.
                stream.Write(data, 0, data.Length);

                // Skriver ut meldingen til konsoll.
                Console.WriteLine("Sent: {0}", message);

                // Lukker strømmen og kobler ifra AppHost. 
                stream.Close();
                AppHost.Close();
            }
            catch (ArgumentNullException e) // Fanger opp feil hvis noen variabler ikke får noen verdi. 
            {
                Console.WriteLine("ArgumentNullException: {0}", e);
            }
            catch (SocketException ex) // Fanger opp feil hvis det skjer noen feil med kommunikasjonen. 
            {
                Console.WriteLine("SocketException: {0}", ex);
            }
        }
    }
}
