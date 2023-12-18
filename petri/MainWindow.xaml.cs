using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Windows.Media.Media3D;
using System.Timers;
using System;
using static petri.PgViewModel;
using System.Threading;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.AccessControl;

namespace petri
{

    class PgViewModel : INotifyPropertyChanged
    {

        //Drawing related
        static object timerLock = new object();
        public static int board = 1000; //width and height of the game field
        public static int bytesperpixel = 4;
        public static int stride = board * bytesperpixel;
        byte[] imgdata = new byte[board * board * bytesperpixel];
        public static WriteableBitmap currentPgImage = new WriteableBitmap(board, board, 96, 96, PixelFormats.Bgr32, null);
        public event PropertyChangedEventHandler PropertyChanged;
        public static Stopwatch calcWatch = new Stopwatch();
        public static Stopwatch cleanupWatch = new Stopwatch();
        public static Stopwatch graphicsWatch = new Stopwatch();
        public static int monitorHits = 0;
        public static int targetFPS = 60;
        public static int lowestFPScalc = 1000000;
        public static int lowestFPSgraph = 1000000;
        //First calculations drop fps for some reason
        public static int skipFirstBunchOfCalculations = 0;
        public WriteableBitmap CurrentPgImage
        {
            get { return currentPgImage; }
            set
            {
                currentPgImage = value;
                PropertyChanged?.Invoke(
                    this, new PropertyChangedEventArgs("CurrentPgImage"));
            }
        }
        //Other global variables

        //Jagged array is faster than 2d array or a single array for my data size
        //Did I properly init static array for a continious memory allocation?
        public static Dot[][] dots = InitDots();
        public static List<Dot> actorsList = new List<Dot>();        
        [ThreadStatic]
        public static Random random = new Random();


        public struct Dot
        {
            public int playerID;
            public int signal;
            public int density;
            public int type; //0 - worker, 1 - warrior, 10 - resource, 11 empty
            //I need x and y despite them being equal to the indexes because I don't know how to store a Dot[][] in actorsList and knowing their index at the same time. Is there any better way?
            public int x;
            public int y;
        }


        public static Dot[][] InitDots()
        {
            //Init jagged array, set borders and coordinates

            Dot[][] result = new Dot[board][];

            for (int x = 0; x < board; ++x)
            {
                result[x] = new Dot[board];
                for (int y = 0; y < board; ++y)
                {
                    result[x][y].x = x;
                    result[x][y].y = y;
                    if (x == 0 || x == board - 1 || y == 0 || y == board - 1)
                        result[x][y].playerID = -1;
                    else
                        result[x][y].playerID = 0;


                }
            }
            return result;
        }


        //Place random dots to start the game
        public void InitPlaceSpawn()
        {

            int randomPosX;
            int randomPosY;

            for (var i = 0; i < 2; i++)
            {
                randomPosX = random.Next(500, 600);
                randomPosY = random.Next(500, 600);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDotOwner(1, randomPosX, randomPosY, 10);
                    actorsList.Add(dots[randomPosX][randomPosY]);
                }
            }

            for (var i = 0; i < 2; i++)
            {
                randomPosX = random.Next(300, 300);
                randomPosY = random.Next(300, 300);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDotOwner(2, randomPosX, randomPosY, 10);
                    actorsList.Add(dots[randomPosX][randomPosY]);
                }
            }
        }

        public void UpdateDotOwner(int playerID, int x, int y, int type)
        {
            byte color;
            dots[x][y].type = type;
            dots[x][y].playerID = playerID;
            if (playerID == 1)
            {
                color = 0x0000064;
                imgdata[x * stride + y * 4 + 0] = color;
                imgdata[x * stride + y * 4 + 1] = color;
                imgdata[x * stride + y * 4 + 2] = color;
                imgdata[x * stride + y * 4 + 3] = 0;
            }
            else if (playerID == 2)
            {
                color = 0x0000064;
                imgdata[x * stride + y * 4 + 0] = color;
                imgdata[x * stride + y * 4 + 1] = 0;
                imgdata[x * stride + y * 4 + 2] = color;
                imgdata[x * stride + y * 4 + 3] = color;
            }
            else
            {
                imgdata[x * stride + y * 4 + 0] = 0;
                imgdata[x * stride + y * 4 + 1] = 0;
                imgdata[x * stride + y * 4 + 2] = 0;
                imgdata[x * stride + y * 4 + 3] = 0;
            }
        }


        public List<Dot> ScanNeighbors(Dot actor)
        {
            List<Dot> decisionList = new List<Dot>();
            for (int sector_x = actor.x - 1; sector_x != actor.x + 2; sector_x++)
            {
                for (int sector_y = actor.y - 1; sector_y != actor.y + 2; sector_y++)
                {
                    if (dots[sector_x][sector_y].playerID != 10)
                    {
                        decisionList.Add(dots[sector_x][sector_y]);
                    }
                }
            }
            return decisionList;
        }




        public void Calc(object source, ElapsedEventArgs e)
        {
            if (!Monitor.TryEnter(timerLock))
            {
                monitorHits++;
                MainWindow.main.monitorHits = monitorHits.ToString();
                return;
            }

            try
            {
                calcWatch.Start();

            //Iterating over list index with count is slower than copying the list altogether, but then we have no index of them and I don't know how to delete it after 

            ///
            /// LinkedList ?
            ///

            //List<Dot> currentActorsList = new List<Dot>();
            //List<int> actorsToDeleteList = new List<int>();
            //currentActorsList.AddRange(actorsList);

            //https://stackoverflow.com/questions/6926554/how-to-quickly-remove-items-from-a-list
                List<int> actorsToDeleteList = new List<int>();
                var actor = actorsList.Count;
                for (var i = 0; i < actor; i++)
                {

                    List<Dot> decisionList = ScanNeighbors(actorsList[i]);
                    if (decisionList.Count != 0)
                    {
                        Random rndDestination = new Random();
                        int r = rndDestination.Next(decisionList.Count);

                        //Random rndMoveOrProcreate = new Random();
                        //if (rndMoveOrProcreate.Next(0, 100) < 5 || decisionList.Count == 1 )
                        if (actorsList[i].type == 10)
                        {
                            UpdateDotOwner(actorsList[i].playerID, decisionList[r].x, decisionList[r].y, 0);
                            actorsList.Add(dots[decisionList[r].x][decisionList[r].y]);
                        }
                        else
                        {
                            UpdateDotOwner(actorsList[i].playerID, decisionList[r].x, decisionList[r].y, 0);
                            actorsList.Add(dots[decisionList[r].x][decisionList[r].y]);
                            UpdateDotOwner(0, actorsList[i].x, actorsList[i].y, 0);
                            actorsToDeleteList.Add(i);
                        }
                    }
                    else
                    {
                        if (actorsList[i].type != 10)
                            //UpdateDotOwner(0, actorsList[i].x, actorsList[i].y);
                            actorsToDeleteList.Add(i);
                    }
                }







                cleanupWatch.Start();
                foreach (int index in actorsToDeleteList.OrderByDescending(i => i))
                    actorsList.RemoveAt(index);
                actorsToDeleteList.Clear();
                cleanupWatch.Stop();
                int cleanupFPS = Convert.ToInt32(1000 / cleanupWatch.Elapsed.TotalMilliseconds);

                //MainWindow.main.cleanupFPS = cleanupFPS.ToString();

                MainWindow.main.actors = actorsList.Count().ToString();

                calcWatch.Stop();
                int calcFPS = Convert.ToInt32(1000 / calcWatch.Elapsed.TotalMilliseconds);
                MainWindow.main.calcFPS = calcFPS.ToString();
                calcWatch.Reset();

                if (lowestFPScalc > calcFPS && skipFirstBunchOfCalculations > 50)
                {
                    lowestFPScalc = calcFPS;
                MainWindow.main.lowestCalcFPS = calcFPS.ToString();
            }

                App.Current.Dispatcher.BeginInvoke((Action)delegate
                    {
                        graphicsWatch.Start();
                        currentPgImage.WritePixels(new Int32Rect(0, 0, board, board), imgdata, stride, 0);
                        graphicsWatch.Stop();
                        int graphicsFPS = Convert.ToInt32(1000 / graphicsWatch.Elapsed.TotalMilliseconds);
                        MainWindow.main.graphicsFPS = (graphicsFPS).ToString(); 
                        graphicsWatch.Reset();

                        if (lowestFPSgraph > graphicsFPS & skipFirstBunchOfCalculations > 50)
                        {
                            lowestFPSgraph = graphicsFPS;
                            MainWindow.main.lowestGraphicsFPS = graphicsFPS.ToString();
                        }
                    });
                skipFirstBunchOfCalculations++;
            }
            finally
            {
                Monitor.Exit(timerLock);
            }
        }


    }
    public partial class MainWindow : Window
    {
        internal static MainWindow main;
        internal string calcFPS
        {
            get { return calcFPScounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { calcFPScounter.Content = value; })); }
        }

        internal string graphicsFPS
        {
            get { return graphicsFPScounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { graphicsFPScounter.Content = value; })); }
        }

        internal string monitorHits
        {
            get { return monitorHitsCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { monitorHitsCounter.Content = value; })); }
        }

        internal string actors
        {
            get { return actorsCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { actorsCounter.Content = value; })); }
        }

        internal string lowestCalcFPS
        {
            get { return lowestCalcFPSCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { lowestCalcFPSCounter.Content = value; })); }
        }

        internal string lowestGraphicsFPS
        {
            get { return lowestGraphicsFPSCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { lowestGraphicsFPSCounter.Content = value; })); }
        }

        internal string cleanupFPS
        {
            get { return cleanupCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { cleanupCounter.Content = value; })); }
        }


        public MainWindow()
        {
            InitializeComponent();
            main = this;
            var viewModel = new PgViewModel();
            DataContext = viewModel;

            viewModel.InitPlaceSpawn();



            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(viewModel.Calc);
            aTimer.Interval = Convert.ToInt32(1000 / targetFPS);
            aTimer.Enabled = true;
            targetFPSCounter.Content = targetFPS.ToString();
        }
    }
}
