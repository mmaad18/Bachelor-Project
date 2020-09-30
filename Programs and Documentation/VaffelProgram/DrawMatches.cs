using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Windows;
using Emgu.CV.XFeatures2D;

namespace VaffelProgramV1
{
    public static class DrawMatches
    {
        //-----------------------------| Variabler |-------------------------------------------------------------

        /// <summary>
        /// X- og Y-koordinat, i tillegg til rotasjonen til objektet blir lagret som en global variabel. Dette er for å 
        /// være sikker på at koordinatene tilhører samme objekt. 
        /// </summary>
        public static double Xmidpoint;
        public static double Ymidpoint;
        public static double RotationZ;

        //-------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Dette er funksjonen 
        /// </summary>
        /// <param name="modelImage"> Referansebilde i formatet Mat </param>
        /// <param name="observedImage"> Bildet som kommer fra kameraet, etter at det har blitt behandlet, i formatet Mat </param>
        /// <param name="matchTime"> Returnerer tiden funksjonen brukte på gjennomføre analyseringen av det observerte bildet. </param>
        /// <param name="modelKeyPoints"> Punkter som KAZE setter på referansebildet </param>
        /// <param name="observedKeyPoints"> Punkter som KAZE setter på det observerte bildet </param>
        /// <param name="matches">  </param>
        /// <param name="mask"></param>
        /// <param name="homography"></param>
        public static void FindMatch(Mat modelImage, Mat observedImage, out long matchTime, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography)
        {
            int k = 2;
            double uniquenessThreshold = 0.80;
            
            Stopwatch watch;
            homography = null;

            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();

            using (UMat uModelImage = modelImage.GetUMat(AccessType.Read))
            using (UMat uObservedImage = observedImage.GetUMat(AccessType.Read))
            {
                KAZE featureDetector = new KAZE();

                //extract features from the object image
                Mat modelDescriptors = new Mat();
                featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);

                watch = Stopwatch.StartNew();

                // extract features from the observed image
                Mat observedDescriptors = new Mat();
                featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);

                // Bruteforce, slower but more accurate
                using (Emgu.CV.Flann.LinearIndexParams ip = new Emgu.CV.Flann.LinearIndexParams())
                using (Emgu.CV.Flann.SearchParams sp = new SearchParams())
                using (BFMatcher matcher = new BFMatcher(DistanceType.L1, false)) 
                {
                    matcher.Add(modelDescriptors);

                    matcher.KnnMatch(observedDescriptors, matches, k, null);
                    mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1); // Cv8U er den eneste som virker?
                    mask.SetTo(new MCvScalar(255));
                    Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

                    int nonZeroCount = CvInvoke.CountNonZero(mask);
                    if (nonZeroCount >= 4)
                    {
                        nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
                            matches, mask, 1.5, 20);

                        if (nonZeroCount >= 4)
                            homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
                                observedKeyPoints, matches, mask, 2);
                    }
                }

                watch.Stop();
            }

            matchTime = watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Draw the model image and observed image, the matched features and homography projection.
        /// </summary>
        /// <param name="modelImage">The model image</param>
        /// <param name="observedImage">The observed image</param>
        /// <param name="matchTime">The output total time for computing the homography matrix.</param>
        /// <returns>The model image and observed image, the matched features and homography projection.</returns>
        public static Mat Draw(Mat modelImage, Mat observedImage, out long matchTime)
        {
            Mat homography;
            VectorOfKeyPoint modelKeyPoints;
            VectorOfKeyPoint observedKeyPoints;

            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
            {
                Mat mask;
                FindMatch(modelImage, observedImage, out matchTime, out modelKeyPoints, out observedKeyPoints, matches,
                   out mask, out homography);

                //Draw the matched keypoints
                Mat result = new Mat();
                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                   matches, result, new MCvScalar(255, 255, 255), new MCvScalar(255, 255, 255), mask);

                #region draw the projected region on the image

                if (homography != null)
                {
                    //draw a rectangle along the projected model
                    Rectangle rect = new Rectangle(System.Drawing.Point.Empty, modelImage.Size);
                    PointF[] pts = new PointF[]
                    {
                  new PointF(rect.Left, rect.Bottom),
                  new PointF(rect.Right, rect.Bottom),
                  new PointF(rect.Right, rect.Top),
                  new PointF(rect.Left, rect.Top)
                    };
                    pts = CvInvoke.PerspectiveTransform(pts, homography);

#if NETFX_CORE
               Point[] points = Extensions.ConvertAll<PointF, Point>(pts, Point.Round);
#else
                    System.Drawing.Point[] points = Array.ConvertAll<PointF, System.Drawing.Point>(pts, System.Drawing.Point.Round);

#endif

                    koordinaterFunksjon(points);

                    using (VectorOfPoint vp = new VectorOfPoint(points))
                    {
                        CvInvoke.Polylines(result, vp, true, new MCvScalar(0, 0, 255, 255), 4); // Siste tallet bestemmer tjukkelsen på linjen som den tegner. 
                                                                                                // Tallene før det definerer fargen og gjennomsiktighet, i formatet Bgra. 
                    }
                }
                #endregion

                return result;

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        public static void koordinaterFunksjon(System.Drawing.Point[] points)
        {
            int j = 0;

            double Xavg = 0.0;
            double Yavg = 0.0;

            double rotAvg = 0.0;
            double length = 0.0;
            double width = 0.0;
            int[] XcoordTable = new int[4];
            int[] YcoordTable = new int[4];
            double[] XnullRot = new double[4];
            double[] YnullRot = new double[4];

            foreach (var x in points)
            {
                string XandY = Convert.ToString(points[j]);

                int xEquals = XandY.IndexOf("=");
                int comma = XandY.IndexOf(",");
                int yEquals = XandY.LastIndexOf("=");
                int XandYend = XandY.IndexOf("}");

                string Xcoord = XandY.Substring(xEquals + 1, comma - xEquals - 1);
                string Ycoord = XandY.Substring(yEquals + 1, XandYend - yEquals - 1);

                int XcoordInt = int.Parse(Xcoord);
                int YcoordInt = int.Parse(Ycoord);

                XcoordTable[j] = XcoordInt;
                YcoordTable[j] = YcoordInt;

                Xavg = Xavg + XcoordInt;
                Yavg = Yavg + YcoordInt;

                j++;
            }
            
            Xavg = Xavg / j;
            Yavg = Yavg / j;

            Xmidpoint = Xavg;
            Ymidpoint = Yavg;

            length = (Math.Sqrt(Math.Pow(XcoordTable[0] - XcoordTable[3], 2) + Math.Pow(YcoordTable[0] - YcoordTable[3], 2)) +
                      Math.Sqrt(Math.Pow(XcoordTable[1] - XcoordTable[2], 2) + Math.Pow(YcoordTable[1] - YcoordTable[2], 2))) / 2;

            width = (Math.Sqrt(Math.Pow(XcoordTable[1] - XcoordTable[0], 2) + Math.Pow(YcoordTable[1] - YcoordTable[0], 2)) +
                      Math.Sqrt(Math.Pow(XcoordTable[2] - XcoordTable[3], 2) + Math.Pow(YcoordTable[2] - YcoordTable[3], 2))) / 2;

            XnullRot[0] = Xavg - (width / 2);
            XnullRot[3] = XnullRot[0];

            XnullRot[1] = Xavg + (width / 2);
            XnullRot[2] = XnullRot[1];

            YnullRot[0] = Yavg + (length / 2);
            YnullRot[1] = YnullRot[0];

            YnullRot[2] = Yavg - (length / 2);
            YnullRot[3] = YnullRot[2];

            double[] vinkelTabell = new double[4];
            double absoluttVerdiGrense = (170.0 / 180.0) * Math.PI;
            bool er180grader = false;

            for (int i = 0; i < j; i++)
            {
                Vector vektorMidTil0ref = new Vector(XnullRot[i] - Xavg, YnullRot[i] - Yavg);
                Vector vektorMidTil0 = new Vector(XcoordTable[i] - Xavg, YcoordTable[i] - Yavg);

                double vinkel = Math.Acos((vektorMidTil0 * vektorMidTil0ref) / (vektorMidTil0.Length * vektorMidTil0ref.Length));

                double kryssprodukt = Vector.CrossProduct(vektorMidTil0, vektorMidTil0ref);

                if (kryssprodukt > 0)
                {
                    vinkel = -vinkel;
                }

                vinkelTabell[i] = vinkel;

                if (i >= 1)
                {
                    if ((((vinkelTabell[i] < 0) && (vinkelTabell[i - 1] > 0)) || ((vinkelTabell[i] > 0) && (vinkelTabell[i - 1] < 0)))
                        && (Math.Abs(vinkelTabell[i]) >= absoluttVerdiGrense) && (Math.Abs(vinkelTabell[i - 1]) >= absoluttVerdiGrense))
                    {
                        er180grader = true;
                    }
                }

                Console.WriteLine("Vektorvinkel: " + (vinkel * (180.0 / Math.PI)) + " #" + i);

                rotAvg = rotAvg + vinkel;
            }

            rotAvg = rotAvg / j;

            if (er180grader == true)
            {
                rotAvg = Math.PI;
            }

            Console.WriteLine("Endelig Rotasjon: " + (rotAvg * (180.0 / Math.PI)));

            RotationZ = rotAvg;

            Console.WriteLine("Xposisjon: " + Xmidpoint);
            Console.WriteLine("Yposisjon: " + Ymidpoint);
            Console.WriteLine("Rotasjon: " + RotationZ);
        }
    }
}
