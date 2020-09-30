//------------------------------------------------------------------------------

///<summary>
///
/// Forfatter: Mohamed Yahya Maad
/// Sist endret: 18/06/2017
/// Versjon: ca. #30
/// 
/// </summary>

//------------------------------------------------------------------------------

namespace VaffelProgramV1
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    
    using Microsoft.Kinect; // Nødvendig referanse for å få Kinect V2 kameraet sine kommandoer til å fungere. 

    using System.Globalization; // Nødvendig referanse for å kunne sette Culture Info. 

    ///<summary>
    /// Nødvendige referanser for å få kommandoene fra EmguCV biblioteket til å fungere. 
    /// </summary>
    using Emgu.CV;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Features2D;
    using Emgu.CV.Structure;
    using Emgu.CV.UI;
    using Emgu.CV.Util;

    ///<summary>
    /// Nødvendige referanser for å kunne behandle bilder/bitmaps. 
    /// </summary>
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    ///<summary>
    /// Nødvendige referanser for å få til å bruke tråder og TCP/IP kommunikasjon. 
    /// </summary>
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text;

    using System.Runtime.InteropServices;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null; 

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        //----------------------------------------| Bilde Delay |--------------------------------------------

        object sync = new object(); // Objekt for synkronisering av forskjellige tråder. Dette brukes for at konverteringen av bilde 
                                    // fra WritableBitmap til Image ikke skal foregå på samme tidspunkt som bildet blir flippet.
                                    // Hvis dette skjer samtidig, så kan man få en exception som sier 
                                    // at her behandles minne som er blitt korrupt.

        //----------------------------------------| Backgroundworker |-----------------------------------------

        BackgroundWorker bgWorker; // Backgroundworkeren blir deklarert 
        string path;               // Deklarerer en string som skal brukes til å definere hvor skjermbilder skal lagres. 

        AutoResetEvent resetEvent; // Event som passer brukes til å passe på at backgroundworkeren er ferdig å analysere bildet 
                                   // før den sender koordinatene til det forespurte objektet tilbake. Dette forsikrer oss om at 
                                   // det alltid blir sendt riktige koordinater til roboten. 

        //----------------------------------------| Server |-----------------------------------------
        
        CultureInfo ci = new CultureInfo("en-US"); // Variabel som presiserer at tekst som sendes skal ha engelsk-Amerikansk formatering.

        //----------------------------------------| Lagring av koordinater |-----------------------------------------


        /// <summary>
        /// Deklarerer string'en som skal inneholde vaffeljernets nødvendige variabler. 
        /// Dette inkluderer X, Y og Z koordinater til hvor håndtaket er for 3 punkter i
        /// sirkelbevegelsen som skal til for å åpne vaffeljernet. 
        /// Variabel #10 er rotasjonen til vaffeljernet. 
        /// </summary>
        string vaffeljernPosisjon; 

        /// <summary>
        /// Deklarerer string'en som skal inneholde bollens nødvendige variabler. 
        /// Dette inkluderer X og Y koordinat, I tillegg til en Z-koordinat som er hardkodet inn.
        /// </summary>
        string bollePosisjon;

        /// <summary>
        /// Deklarerer string'en som skal inneholde fatets nødvendige variabler. 
        /// Dette inkluderer X og Y koordinat, I tillegg til en Z-koordinat som er hardkodet inn.
        /// </summary>
        string fatPosisjon;

        /// <summary>
        /// Deklarerer string'en som skal inneholde jernets senterposisjon sinevariabler. 
        /// Dette inkluderer X og Y koordinat, en Z-koordinat som er hardkodet inn, i tillegg til rotasjonen til jernet
        /// Rotasjonen til jernet bestemmer om roboten skal 
        /// </summary>
        string jernMidtPosisjon;

        string testPosisjon;

        //---------------------------------------| Det flippede bildet |-------------------------------------------------------

        Image<Bgr, byte> flipBilde; // Deklarerer bildet som blir flippet om den vertikale aksen før det blir kuttet. 
                                    // Dette er så man slipper å tenke motsatt vei når man setter opp faste referansepunkt, siden bildet 
                                    // som kamera leverer er speilet om den vertikale aksen, sammenlignet med virkeligheten. 

        //---------------------------------------| ROI |------------------------------------------------------------------------

        private Image<Bgr, byte> innkommendeBilde; // Deklarerer et bilde som skal inneholde bildet som kommer fra kameraet, etter at det har blitt 
                                                   // konvertert fra WritableBitmap til Image. 

        private Rectangle ønsketROI = new Rectangle(); // Deklarerer rektangelen som definerer hvor stort område vi vil ta med videre. 
                                                       // Fordelen med dette er at det blir mindre å analyse, lettere å konvertere fra piksler 
                                                       // til meter, og det blir mindre støy. 
                                                       
        //---------------------------------------| Crop |--------------------------------------------------

        Image<Bgr, byte> beskjærtBilde; // Deklarer variabelen for bildet som skal analyseres. Etter at bildet blir flippet om den vertikale aksen,
                                        // så blir bildet beskjært for å kutte vekk unødvendige piksler. I tilfellene våres så har vi kunne kuttet 
                                        // vekk 70% av bildet. Dette gjør at det blir mindre sannsynlighet for at programmet leser av bildet slik
                                        // an det gir dårlige resultater for posisjon og rotasjon. I tillegg så blir analyseringstiden også kuttet
                                        // med 70%, siden det blir færre piksler å analysere. 
                                        
        //---------------------------------------| Høydekompensering |--------------------------------------------------
        
        /// <summary>
        /// Grunnen til at det må kompenseres for høyden på objektet er at det fra kameraet sitt 2D-perspektiv ser ut som om objektet 
        /// er lengre ut mot siden enn det de egentlig er. Dette har er fordi toppen på objektet er det som blir funnet, men det det er
        /// basen på objektet som har posisjonen til objektet. 
        /// </summary>
        double objektHøyde;                     // Variablen for høyden til objektet som skal finnes blir deklarert. 
        const double kameraHøyde = 1.265;       // Dette er høyden kameraet sin linse er over bordet, i meter. Denne variablen må endres hvis 
                                                // høyden på kamerastativet blir endret. 

        //---------------------------------------| Testing av tider på algoritmer |--------------------------------------------------

        //StreamWriter utFil;
        //int teller = 0;
        //double gjennomsnitt = 0;

        //string algoritmeNavn;
        //string objektNavn;
        //string filNavn;
        //string posisjonNavn;
        //string rotasjonNavn;

        //--------------------------------------------------------------------------------------------------------------------------

        Kommunikasjon komm; // Deklarerer en variabel av typen Kommunikasjon, som er klassen som inneholder fuksjonene for 
                            // kommunikasjon over TCP/IP. 

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            ///<summary>
            /// Grunnen til at vi lager en pointer til hovedvinduet fra klassen Kommunikasjon, er slik at klassen kan referere til funksjoner, variabler 
            /// og GUI-enheter. Dette gjør at vi slipper å mellomlagre mye data i globale variabler. 
            /// </summary>
            Kommunikasjon.hovedVindu = this; // hovedVindu sin pointer blir satt til dette vinduet når vi skriver " = this". 
            komm =  new Kommunikasjon();     // Deretter lager vi ett nytt objekt av typen Kommunikasjon. Dermed kan vi refere til dette objektet 
                                             // når vi vil bruke funksjoner og variabler fra klassen kommunikasjon. 

            //----------------------------------------------------------------------------------------------------------

            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;


            //----------------------------------------| Lagring av koordinater |-----------------------------------------

            // Verdiene som disse variablene har nå er helt feil, så ikke tenk på dem. De kommer til å få en brukbar verdi senere i programmet. 
            vaffeljernPosisjon = string.Format(ci, "({0}, {1}, {2}, {3})", 0.0, 0.600, 0.4, 0.0); // Variablen for vaffeljernet sin posisjon blir opprettet. 
            bollePosisjon = string.Format(ci, "({0}, {1}, {2}, {3})", 0.0, 0.600, 0.4, 0.0);      // Variablen for bollen sin posisjon blir opprettet. 
            fatPosisjon = string.Format(ci, "({0}, {1}, {2}, {3})", 0.0, 0.600, 0.4, 0.0);        // Variablen for fatet sin posisjon blir opprettet. 

            testPosisjon = string.Format(ci, "({0}, {1}, {2}, {3})", 0.0, 0.600, 0.4, 0.0);

            //----------------------------------------| Backgroundworker |-----------------------------------------

            /// <summary>
            /// Standard prosedyre for å opprette en backgroundworker i WPF.
            /// </summary>
            bgWorker = new BackgroundWorker();
            bgWorker.DoWork += bgWorker_DoWork;
            bgWorker.RunWorkerCompleted += bgWorker_RunWorkerCompleted;

            //-----------------------------------------| Synkronisering av backgroundworker |----------------------------------------------

            /// <summary>
            /// resetEvent blir brukt for at tråden som sender koordinater til roboten ikke skal sende svar før backgroundworker 
            /// er ferdig å analysere bildet fra kameraet. 
            /// </summary>
            resetEvent = new AutoResetEvent(false); // Oppretter en ny AutoResetEvent, som blokkerer tråder når den er i "Reset" modus. 
            resetEvent.Set();                       // Derfor setter vi den til "Set" nå, slik at den ikke skal blokkere noen tråder mens
                                                    // starter opp. 

            //----------------------------------------| Testing av hastighet på algoritmer |-----------------------------------------

            //algoritmeNavn = "KAZE";
            //objektNavn = "Vaffeljern";
            //posisjonNavn = "_Pos0";
            //rotasjonNavn = "_Rot0";

            //filNavn = algoritmeNavn + "_tider_" + objektNavn + posisjonNavn + rotasjonNavn + ".txt";

            //utFil = File.CreateText("AlgoritmeTester\\" + filNavn);
            //utFil.WriteLine("Dette er en test for tidene " + algoritmeNavn + " bruker på å finne: " + objektNavn + ".\n");

            //-----------------------------------------------------------------------------------------------------------
            
            this.InitializeComponent(); // Initialiserer GUI-komponenter. Dette betyr at manipulering av GUI-elementer må skje etter denne hendelsen. 

            //------------------------------------------------| Knapper |-----------------------------------------

            /// <summary> 
            /// Gjør diverse knapper tilgjengelige og utilgjengelige, slik at programmet skal bli mer idiotsikkert. 
            /// </summary>
            btnServerStart.IsEnabled = true; // Start-knappen blir tilgjengelig. 
            btnServerStop.IsEnabled = false; // Stopp-knappen blir utilgjengelig. 

            btnDashConn.IsEnabled = true;    // Knappen for å koble til dashboard server blir tilgjengelig. 
            btnDashDisc.IsEnabled = false;   // Knappen for å koble fra dashboard server blir utilgjengelig. 

            btnPlayRobot.IsEnabled = true;   // Knappen for å starte roboten blir tilgjengelig. 
            btnStopRobot.IsEnabled = false;  // Knappen for å stoppe roboten blir utilgjengelig. 

            //----------------------------------------------------------------------------------------------------------

            objektHøyde = 0.0; // Høyden på objektet blir satt til 0,0 meter mens programmet starter opp. 

            path = ""; // Stien for hvor skjermbilder skal lagrer blir satt til å være tom når programmet starter opp. 
        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.colorBitmap;
            }
        }

        /// <summary>
        /// Dette er backgroundworkeren. Den tar seg av analyseringen av bilder, og passer på at analyseringen er ferdig
        /// før koordinatene til et objekt blir lagret i de globale variablene til klassen DrawMatches. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="bildeNavn"> Navnet på referansebildet som skal brukes. </param>
        private void bgWorker_DoWork(object sender, DoWorkEventArgs bildeNavn)
        {
            Console.WriteLine("Backgroundworker kjører...");

            long matchtime = 0; // Deklarerer og oppretter en variabel som skal lagre hvor lang tid algoritmen brukte på å utføre jobben sin. Dette ble 
                                // gjort for å kunne sammenlikne hvor lang tid de forskjellige algoritmene brukte. 

            Mat resultat;
            string requestedObject = (string)bildeNavn.Argument; // Tar inn ett argument som er navnet på bildet den skal bruke. Siden "bildeNavn" kommer
                                                                 // inn som et objekt, må den "castes" som typen string for at det ikke skal oppstå en 
                                                                 // konflikt i variabelformat. 

            ønsketROI.Location = new System.Drawing.Point(getROIstartX(), getROIstartY());
            ønsketROI.Size = new System.Drawing.Size(getROILengdeX(), getROILengdeY());

            Monitor.Enter(sync); // Monitor
            flipBilde = innkommendeBilde.Flip(FlipType.Horizontal);
            Monitor.Exit(sync);

            beskjærtBilde = flipBilde.Rotate(getBildeRotasjon(), new Bgr(0.0, 0.0, 0.0));
            beskjærtBilde.ROI = ønsketROI;

            using (Mat modelImage = CvInvoke.Imread(requestedObject, ImreadModes.Color))
            using (Mat observedImage = beskjærtBilde.Mat)
            {

                resultat = DrawMatches.Draw(modelImage, observedImage, out matchtime);

                resetEvent.Set();

                //teller++;
                //utFil.WriteLine("Forsøk #" + teller + ". Tid brukt på å finne " + objektNavn + ": " + matchtime + " ms");
                //gjennomsnitt += matchtime;
                //Console.WriteLine("Teller: " + teller);

                Thread visningstråd = new Thread(new ParameterizedThreadStart(visBilde));
                visningstråd.Start(resultat);
            }
        }

        private void bgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Oppgave fullført...");
            //beskjærtBilde.Save(Path.Combine("C:\\Users\\Yahya\\Google Drive\\Bachelorprosjekt\\Finpussing\\VPuInstallerV2\\Images", "referanseBilde.png"));
        }

        private void visBilde(object bilde)
        {
            CvInvoke.Imshow("Window showing a picture", (Mat)bilde);
            CvInvoke.WaitKey(0);
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e) // Dette skjer når vinduet lukker seg
        {
            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }

            //gjennomsnitt = gjennomsnitt / teller;
            //utFil.WriteLine("\nGjennomsnittelig tid: " + gjennomsnitt + " ms");
            //utFil.Close();
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// 
        /// Dette er hendelser som skjer hver gang et nytt frame blir levert til Kinect V2 kameraet. 
        /// 
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)  
        {
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    var frameData = new byte[colorFrameDescription.Width * colorFrameDescription.Height * PixelFormats.Bgra32.BitsPerPixel / 8];
                    colorFrame.CopyConvertedFrameDataToArray(frameData, ColorImageFormat.Bgra);

                    var colorFrameBitmap = new Bitmap(colorFrameDescription.Width, 
                                                      colorFrameDescription.Height, 
                                                      System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                    var frameBitmapData = colorFrameBitmap.LockBits(new Rectangle(0, 0, colorFrameBitmap.Width, colorFrameBitmap.Height),
                                                                    ImageLockMode.WriteOnly,
                                                                    colorFrameBitmap.PixelFormat);

                    Marshal.Copy(frameData, 0, frameBitmapData.Scan0, frameData.Length);
                    colorFrameBitmap.UnlockBits(frameBitmapData);

                    Monitor.Enter(sync);
                    innkommendeBilde = new Image<Bgr, byte>(colorFrameBitmap);
                    Monitor.Exit(sync);


                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();

                    }
                }
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : "Kinect V2 kameraet er ikke tilgjengelig...";
        }

        //------------------------------------------------------| Koordinater |----------------------------------------------------

        /// <summary>
        /// 
        /// </summary>
        /// <param name="midtpunkt"></param>
        /// <returns></returns>
        double getX(double midtpunkt) // Får X-koordinatet i piksler og leverer det tilbake i meter. 
        {
            double tallX = 0.0;
            const double lengde = 1.05;
            double utgangspunkt = -0.38183;

            double vinkel = 0.0;
            double egentligMidtpunkt = 0.0;

            double midtMeter = (midtpunkt * koeffisientX(lengde));

            if (midtMeter <= (lengde / 2))
            {
                vinkel = Math.Atan2((lengde / 2) - midtMeter, kameraHøyde); //motstående, så hosliggende

                egentligMidtpunkt = utgangspunkt + (lengde / 2) - ((kameraHøyde - objektHøyde) * Math.Tan(vinkel));
            }
            else
            {
                vinkel = Math.Atan2(midtMeter - (lengde / 2), kameraHøyde); //motstående, så hosliggende

                egentligMidtpunkt = utgangspunkt + (lengde / 2) + ((kameraHøyde - objektHøyde) * Math.Tan(vinkel));
            }

            if (egentligMidtpunkt >= (utgangspunkt + lengde))
            {
                egentligMidtpunkt = utgangspunkt + lengde;
            }
            else if (egentligMidtpunkt <= utgangspunkt)
            {
                egentligMidtpunkt = utgangspunkt;
            }

            this.Dispatcher.Invoke(() =>
            {
                txtX.Text = Convert.ToString(egentligMidtpunkt);

                Double.TryParse(txtX.Text, out tallX);
            });

            return tallX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="midtpunkt"></param>
        /// <returns></returns>
        double getY(double midtpunkt) // Får Y-koordinatet i piksler og leverer det tilbake i meter. 
        {
            double tallY = 0.0;
            const double bredde = 0.685;
            double utgangspunkt = 0.50087;

            double vinkel = 0.0;
            double egentligMidtpunkt = 0.0;

            double midtMeter = (midtpunkt * koeffisientY(bredde));

            if (midtMeter <= (bredde / 2))
            {
                vinkel = Math.Atan2((bredde / 2) - midtMeter, kameraHøyde); //motstående, så hosliggende

                egentligMidtpunkt = (bredde / 2) + ((kameraHøyde - objektHøyde) * Math.Tan(vinkel)) + utgangspunkt;
            }
            else
            {
                vinkel = Math.Atan2(midtMeter - (bredde / 2), kameraHøyde); //motstående, så hosliggende

                egentligMidtpunkt = (bredde / 2) - ((kameraHøyde - objektHøyde) * Math.Tan(vinkel)) + utgangspunkt;
            }

            if (egentligMidtpunkt >= (utgangspunkt + bredde))
            {
                egentligMidtpunkt = utgangspunkt + bredde;
            }
            else if (egentligMidtpunkt <= utgangspunkt)
            {
                egentligMidtpunkt = utgangspunkt;
            }

            this.Dispatcher.Invoke(() =>
            {
                txtY.Text = Convert.ToString(egentligMidtpunkt);

                Double.TryParse(txtY.Text, out tallY);
            });

            return tallY;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        double getZ() // Får Z-koordinatet i piksler og leverer det tilbake i meter. 
        {
            double tallZ = 0.0;

            this.Dispatcher.Invoke(() =>
            {
                Double.TryParse(txtZ.Text, out tallZ);
            });

            return tallZ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        double getRotZ() // Får rotasjonen til objektet og setter verdien inn i tekstboksen. 
        {
            double tallRotZ = 0.0;

            this.Dispatcher.Invoke(() =>
            {
                txtRotZ.Text = Convert.ToString(DrawMatches.RotationZ);

                Double.TryParse(txtRotZ.Text, out tallRotZ);
            });

            return tallRotZ;
        }

        //----------------------------------------------| Pixler til meter |-----------------------------------------
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="lengdeMeter"></param>
        /// <returns></returns>
        double koeffisientX(double lengdeMeter) // Konverterer fra piksel til meter i X-akse
        {
            return lengdeMeter / getROILengdeX();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="breddeMeter"></param>
        /// <returns></returns>
        double koeffisientY(double breddeMeter) // Konverterer fra piksel til meter i Y-akse
        {
            return breddeMeter / getROILengdeY();
        }


        //----------------------------------------------| ROI |------------------------------------------------------

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        int getROIstartX() // Setter startposisjon til ROI-bilde i X-akse. 
        {
            int startX = 0;

            this.Dispatcher.Invoke(() =>
            {
                Int32.TryParse(txtROIx.Text, out startX);
            });

            return startX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        int getROIstartY() // Setter startposisjon til ROI-bilde i Y-akse. 
        {
            int startY = 0;

            this.Dispatcher.Invoke(() =>
            {
                Int32.TryParse(txtROIy.Text, out startY);
            });

            return startY;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        int getROILengdeX() // Setter lengde til ROI-bilde i X-akse. 
        {
            int lengdeX = 0;

            this.Dispatcher.Invoke(() =>
            {
                Int32.TryParse(txtROILengdeX.Text, out lengdeX);
            });

            return lengdeX;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        int getROILengdeY() // Setter lengde til ROI-bilde i Y-akse. 
        {
            int lengdeY = 0;

            this.Dispatcher.Invoke(() =>
            {
                Int32.TryParse(txtROILengdeY.Text, out lengdeY);
            });

            return lengdeY;
        }

        //----------------------------------------------| Rotasjon av bilde |------------------------------------------------------

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        double getBildeRotasjon() // Setter rotasjon til ROI-bilde. 
        {
            double rotasjonGrader = 0;

            this.Dispatcher.Invoke(() =>
            {
                Double.TryParse(txtBildeRotasjon.Text, out rotasjonGrader);
            });

            return rotasjonGrader;
        }
        

        //------------------------------------------| Logikk |------------------------------------------------------

        /// <summary>
        /// 
        /// </summary>
        /// <param name="melding"></param>
        /// <returns></returns>
        public string ReturFunksjon(string melding) // Dette er funksjonen som bestemmer hva som skal returneres tilbake til roboten. 
        {

            double JsirkelRadius = 0.361;
            double JobjektLengde = 0.360;

            double offsetZ1 = 0.10; // Punkt 10 cm over før den går til håndtaket. 

            double JoffsetGrader = 12.0; // Antall grader håndtaket starter over rotasjonspunktet. 

            double Jpunkt1Grader = 0.0 + JoffsetGrader;
            double Jpunkt3Grader = 95.5 + JoffsetGrader;
            double Jpunkt2Grader = Jpunkt3Grader / 2.0;

            string stringSvar = "Ugyldig forespørsel";

            switch (melding)
            {
                case "$test":
                    if (bgWorker.IsBusy != true)
                    {
                        resetEvent.Reset();

                        objektHøyde = 0.003; // 0.003 meter høy 

                        bgWorker.RunWorkerAsync("TestObject.png");
                        resetEvent.WaitOne();
                        testPosisjon = string.Format(ci, "({0}, {1}, {2})", getX(DrawMatches.Xmidpoint),
                                                                             getY(DrawMatches.Ymidpoint),
                                                                             0.40);
                        resetEvent.Set();
                    }

                    stringSvar = testPosisjon;
                    break;

                case "$bolle":
                    if (bgWorker.IsBusy != true)
                    {
                        resetEvent.Reset();

                        objektHøyde = 0.215; // 0.215 meter høy 

                        bgWorker.RunWorkerAsync("bolle.png");
                        resetEvent.WaitOne();
                        bollePosisjon = string.Format(ci, "({0}, {1}, {2})", getX(DrawMatches.Xmidpoint),
                                                                             getY(DrawMatches.Ymidpoint),
                                                                             0.55);
                        resetEvent.Set();
                    }

                    stringSvar = bollePosisjon;
                    break;

                case "$fat":
                    if (bgWorker.IsBusy != true)
                    {
                        resetEvent.Reset();

                        objektHøyde = 0.025;

                        bgWorker.RunWorkerAsync("fat.png");
                        resetEvent.WaitOne();
                        fatPosisjon = string.Format(ci, "({0}, {1}, {2})", getX(DrawMatches.Xmidpoint),
                                                                           getY(DrawMatches.Ymidpoint),
                                                                           0.30);
                        resetEvent.Set();
                    }

                    stringSvar = fatPosisjon;
                    break;

                case "$jern":
                    if (bgWorker.IsBusy != true)
                    {
                        resetEvent.Reset();

                        objektHøyde = 0.110;

                        bgWorker.RunWorkerAsync("vaffeljern.png");
                        resetEvent.WaitOne();
                        vaffeljernPosisjon = string.Format(ci, "({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
                                                               getX(DrawMatches.Xmidpoint) + JhalvPunktX(JobjektLengde, JsirkelRadius, Jpunkt1Grader, DrawMatches.RotationZ),
                                                               getY(DrawMatches.Ymidpoint) + JhalvPunktY(JobjektLengde, JsirkelRadius, Jpunkt1Grader, DrawMatches.RotationZ),
                                                               getZ() + JhalvPunktZ(JsirkelRadius, Jpunkt1Grader) + offsetZ1, // Her slutter punkt #1

                                                               getX(DrawMatches.Xmidpoint) + JhalvPunktX(JobjektLengde, JsirkelRadius, Jpunkt2Grader, DrawMatches.RotationZ),
                                                               getY(DrawMatches.Ymidpoint) + JhalvPunktY(JobjektLengde, JsirkelRadius, Jpunkt2Grader, DrawMatches.RotationZ),
                                                               getZ() + JhalvPunktZ(JsirkelRadius, Jpunkt2Grader), // Her slutter punkt #2

                                                               getX(DrawMatches.Xmidpoint) + JhalvPunktX(JobjektLengde, JsirkelRadius, Jpunkt3Grader, DrawMatches.RotationZ),
                                                               getY(DrawMatches.Ymidpoint) + JhalvPunktY(JobjektLengde, JsirkelRadius, Jpunkt3Grader, DrawMatches.RotationZ),
                                                               getZ() + JhalvPunktZ(JsirkelRadius, Jpunkt3Grader), // Her slutter punkt #3
                                                               getRotZ()); // Rotasjonen til griperen
                        resetEvent.Set();
                    }

                    stringSvar = vaffeljernPosisjon;
                    break;

                case "$jsenter":
                    if (bgWorker.IsBusy != true)
                    {
                        resetEvent.Reset();
                        bgWorker.RunWorkerAsync("vaffeljern.png");
                        resetEvent.WaitOne();
                        jernMidtPosisjon = string.Format(ci, "({0}, {1}, {2}, {3})",
                                                          getX(DrawMatches.Xmidpoint) + XlengdeOffset(-0.05, DrawMatches.RotationZ),
                                                          getY(DrawMatches.Ymidpoint) + YlengdeOffset(-0.05, DrawMatches.RotationZ),
                                                          0.55, getRotZ());
                        resetEvent.Set();
                    }

                    stringSvar = jernMidtPosisjon;
                    break;

                default:
                    stringSvar = "Ugyldig forespørsel";
                    break;
            }

            return stringSvar;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnServerStart_Click(object sender, RoutedEventArgs e)
        {
            btnServerStart.IsEnabled = false;
            btnServerStop.IsEnabled = true;

            komm.lytter = new TcpListener(IPAddress.Any, Convert.ToInt32(txtServerPortNr.Text)); // Lytteren lytter etter meldinger fra hvilken så helst IP på Port#30000.
            komm.lytter.Start(); // Lyttingen starter
            komm.lytteTråd = new Thread(komm.TCPlytter); // Lyttere blir satt i egne tråder, som blir startet etter hvert som nye klienter kobler seg til. 
            komm.lytteTråd.Start(); // Tråden for lytting blir startet. 

            komm.AppLytter = new TcpListener(IPAddress.Any, 30001);
            komm.AppLytter.Start();
            komm.AppLytteTråd = new Thread(komm.AppServerLytter);
            komm.AppLytteTråd.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnServerStop_Click(object sender, RoutedEventArgs e)
        {
            btnServerStart.IsEnabled = true;
            btnServerStop.IsEnabled = false;

            komm.lytter.Stop();

            komm.AppLytter.Stop();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPlayRobot_Click(object sender, RoutedEventArgs e)
        {
            if (btnDashConn.IsEnabled == false && btnDashDisc.IsEnabled == true)
            {
                btnPlayRobot.IsEnabled = false;
                btnStopRobot.IsEnabled = true;

                komm.StartRobotFunksjon();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStopRobot_Click(object sender, RoutedEventArgs e)
        {
            if (btnDashConn.IsEnabled == false && btnDashDisc.IsEnabled == true)
            {
                btnPlayRobot.IsEnabled = true;
                btnStopRobot.IsEnabled = false;

                komm.StoppRobotFunksjon();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDashConn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                komm.dashConnectTråd = new Thread(komm.Connect);
                komm.dashConnectTråd.Start();

                btnDashConn.IsEnabled = false;
                btnDashDisc.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDashDisc_Click(object sender, RoutedEventArgs e)
        {
            if (komm.dashbordKlient == null) return;
            if (komm.dashbordKlient.Connected) komm.dashbordKlient.Close();

            btnDashConn.IsEnabled = true;
            btnDashDisc.IsEnabled = false;

            btnPlayRobot.IsEnabled = true;
            btnStopRobot.IsEnabled = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objektLengde"></param>
        /// <param name="rotasjonZ"></param>
        /// <returns></returns>
        double XlengdeOffset(double objektLengde, double rotasjonZ)
        {
            double returVerdi = 0.0;

            returVerdi = objektLengde * Math.Sin(rotasjonZ);

            return returVerdi;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objektLengde"></param>
        /// <param name="rotasjonZ"></param>
        /// <returns></returns>
        double YlengdeOffset(double objektLengde, double rotasjonZ)
        {
            double returVerdi = 0.0;

            returVerdi = objektLengde * Math.Cos(rotasjonZ);

            return returVerdi;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objektLengde"></param>
        /// <param name="sirkelRadius"></param>
        /// <param name="rotasjonXY"></param>
        /// <param name="rotasjonZ"></param>
        /// <returns></returns>
        double JhalvPunktX(double objektLengde, double sirkelRadius, double rotasjonXY, double rotasjonZ)
        {
            double returVerdi = 0.0;

            double vinkelXY = (rotasjonXY * Math.PI) / 180;

            returVerdi = ((objektLengde / 2) * Math.Sin(rotasjonZ)) - (sirkelRadius * (1 - Math.Cos(vinkelXY)) * Math.Sin(rotasjonZ));

            return returVerdi;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objektLengde"></param>
        /// <param name="sirkelRadius"></param>
        /// <param name="rotasjonXY"></param>
        /// <param name="rotasjonZ"></param>
        /// <returns></returns>
        double JhalvPunktY(double objektLengde, double sirkelRadius, double rotasjonXY, double rotasjonZ)
        {
            double returVerdi = 0.0;

            double vinkelXY = (rotasjonXY * Math.PI) / 180;

            returVerdi = ((objektLengde / 2) * Math.Cos(rotasjonZ)) - (sirkelRadius * (1 - Math.Cos(vinkelXY)) * Math.Cos(rotasjonZ));

            return returVerdi;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sirkelRadius"></param>
        /// <param name="rotasjonXY"></param>
        /// <returns>
        /// 
        /// </returns>
        double JhalvPunktZ(double sirkelRadius, double rotasjonXY)
        {
            double returVerdi = 0.0;

            double vinkelXY = (rotasjonXY * Math.PI) / 180;

            returVerdi = sirkelRadius * Math.Sin(vinkelXY);

            return returVerdi;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStorskjerm_Click(object sender, RoutedEventArgs e)
        {
            if (KnapperHøyre.IsEnabled == false || KnapperVenstre.IsEnabled == false)
            {
                KnapperHøyre.IsEnabled = true;
                KnapperHøyre.Opacity = 100;

                KnapperVenstre.IsEnabled = true;
                KnapperVenstre.Opacity = 100;

            }
            else
            {
                KnapperHøyre.IsEnabled = false;
                KnapperHøyre.Opacity = 0;

                KnapperVenstre.IsEnabled = false;
                KnapperVenstre.Opacity = 0;

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSkjermbilde_Click(object sender, RoutedEventArgs e)
        {
            if (this.colorBitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

                path = Path.Combine("C:\\Users\\Yahya\\Google Drive\\Bachelorprosjekt\\Innlevert Prog og Dok\\VaffelProgram\\Images", "DetNavnetDuVilHaPåBildet.png");

                // Prøver å skrive ny fil til disk
                try
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.StatusText = string.Format(Properties.Resources.SavedScreenshotStatusTextFormat, path);
                }
                catch (IOException)
                {
                    this.StatusText = string.Format(Properties.Resources.FailedScreenshotStatusTextFormat, path);
                }
            }
        }
    }
}
