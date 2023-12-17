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
            public int targetDot;
            public int action;
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
                    {
                        result[x][y].playerID = -1;
                    }
                    else
                        result[x][y].playerID = 0;

                }
            }
            return result;
        }


        //Place random dots to start the game
        public void InitPlaceDot()
        {

            int randomPosX;
            int randomPosY;

            for (var i = 0; i < 1; i++)
            {
                randomPosX = random.Next(500, 501);
                randomPosY = random.Next(500, 501);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    PlaceDot(1, randomPosX, randomPosY);
                }
            }
        }

        //Method for additng dots in running game
        public void PlaceDot(int playerID, int x, int y)
        {
            dots[x][y].playerID = playerID;
            dots[x][y].x = x;
            dots[x][y].y = y;
            actorsList.Add(dots[x][y]);
            imgdata[x * stride + y * 4 + 0] = Convert.ToByte((100));
            imgdata[x * stride + y * 4 + 1] = Convert.ToByte((100));
            imgdata[x * stride + y * 4 + 2] = Convert.ToByte((100));
            imgdata[x * stride + y * 4 + 3] = Convert.ToByte((100));
        }

        public void RemoveActors(List<int> removeActorsList)
        {
            foreach (int index in removeActorsList.OrderByDescending(i => i))
            actorsList.RemoveAt(index);

        }
        public void Calc(object source, ElapsedEventArgs e)
        {
            //Prevent multiple threads to add items in a single list
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
                List<int> removeActorsList = new List<int>();
                var k = actorsList.Count;
                for (var i = 0; i < k; i++)
                {
                    //List of possible directions to grow for this Dot
                    List<Dot> decisionList = new List<Dot>();

                    for (int sector_x = actorsList[i].x - 1; sector_x != actorsList[i].x + 2; sector_x++)
                    {
                        for (int sector_y = actorsList[i].y - 1; sector_y != actorsList[i].y + 2; sector_y++)
                        {
                            if (dots[sector_x][sector_y].playerID == 0)
                            {
                                decisionList.Add(dots[sector_x][sector_y]);
                            }
                        }
                    }

                    if (decisionList.Count != 0)
                    {
                        //Dot randomTarget = new Dot();
                        //remove this random and fix already existing static random
                        Random rnd = new Random();
                        int r = rnd.Next(decisionList.Count);

                        //randomTarget = decisionList[r];
                        PlaceDot(1, decisionList[r].x, decisionList[r].y);
                    }
                    else
                    {
                        removeActorsList.Add(i);
                    }
                }
                RemoveActors(removeActorsList);

                MainWindow.main.actors = k.ToString();

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


        public MainWindow()
        {
            InitializeComponent();
            main = this;
            var viewModel = new PgViewModel();
            DataContext = viewModel;

            viewModel.InitPlaceDot();

            //FPS example
            //timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1f / 120f) };
            //timer.Tick += TimerTick;
            //timer.Start();


            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(viewModel.Calc);
            aTimer.Interval = Convert.ToInt32(1000 / targetFPS);
            aTimer.Enabled = true;
            targetFPSCounter.Content = targetFPS.ToString();
        }
    }
}




//List<Dot> previousActorsList = new List<Dot>(actorsList);
//foreach (var actor in previousActorsList)
//for (var i = 0; i < actorsList.Count; i++) -- funny results


//for (int sector_x = actor.x - 1; sector_x != actor.x + 2; sector_x++)
//  {
// for (int sector_y = actor.y - 1; sector_y != actor.y + 2; sector_y++)

/*            for (int row = 0; row < board; row++)
            {
                for (int col = 0; col < board; col++)
                {
                    if (dots[row][col].playerID == 1)
                    {
                        imgdata[row * stride + col * 4 + 0] = Convert.ToByte((100));
                        imgdata[row * stride + col * 4 + 1] = Convert.ToByte((100) );
                        imgdata[row * stride + col * 4 + 2] = Convert.ToByte((100) );
                        imgdata[row * stride + col * 4 + 3] = Convert.ToByte((100) );
                    }
                    else
                    {
                        imgdata[row * stride + col * 4 + 0] = Convert.ToByte(0xff);
                        imgdata[row * stride + col * 4 + 1] = Convert.ToByte(0xff);
                        imgdata[row * stride + col * 4 + 2] = Convert.ToByte(0xff);
                        imgdata[row * stride + col * 4 + 3] = Convert.ToByte(0xff);
                    }
                }
              }*/
