using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace NeuralNetwork1
{
    public partial class NeuralNetworksStand : Form
    {
        GenerateImage generator = new GenerateImage();
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;

        private DateTime lastRecognitionTime = DateTime.MinValue;
        private TimeSpan recognitionInterval = TimeSpan.FromSeconds(0.5);
        private DateTime _lastUiUpdate = DateTime.MinValue;

        private volatile bool _isTraining = false;

        // Ползунок 1: порог
        private TrackBar _thresholdBar;
        private Label _thresholdLabel;

        // Ползунок 2: заполнение дыр
        private TrackBar _holesBar;
        private Label _holesLabel;

        public BaseNetwork Net
        {
            get
            {
                var selectedItem = (string)netTypeBox.SelectedItem;
                if (!networksCache.ContainsKey(selectedItem))
                    networksCache.Add(selectedItem, CreateNetwork(selectedItem));
                return networksCache[selectedItem];
            }
        }

        private readonly Dictionary<string, Func<int[], BaseNetwork>> networksFabric;
        private Dictionary<string, BaseNetwork> networksCache = new Dictionary<string, BaseNetwork>();

        public NeuralNetworksStand(Dictionary<string, Func<int[], BaseNetwork>> networksFabric)
        {
            InitializeComponent();
            this.networksFabric = networksFabric;

            netTypeBox.Items.AddRange(this.networksFabric.Keys.Select(s => (object)s).ToArray());
            netTypeBox.SelectedIndex = 0;

            netStructureBox.Text = "4096;1200;12";
            classCounter.Value = 12;
            generator.FigureCount = 12;

            InitCamera();
            button3_Click(this, null);

            InitPreprocessUi(); // <- добавили ползунки

            if (Properties.Resources.ResourceManager.GetObject("Title") != null)
                pictureBox1.Image = Properties.Resources.Title;

            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.AutoScroll = true;
            this.MinimumSize = new Size(1000, 700);
        }

        private void InitPreprocessUi()
        {
            // Под окошком debugPictureBox ("Видит сеть")
            _thresholdLabel = new Label
            {
                AutoSize = true,
                Text = $"Порог: {ImageProcessor.PixelThreshold}",
                Location = new Point(debugPictureBox.Left, debugPictureBox.Bottom + 10)
            };

            _thresholdBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 255,
                Value = Math.Max(0, Math.Min(255, ImageProcessor.PixelThreshold)),
                TickFrequency = 25,
                SmallChange = 1,
                LargeChange = 10,
                Width = 320,
                Location = new Point(debugPictureBox.Left, _thresholdLabel.Bottom + 4)
            };

            _thresholdBar.ValueChanged += (s, e) =>
            {
                ImageProcessor.PixelThreshold = _thresholdBar.Value;
                _thresholdLabel.Text = $"Порог: {ImageProcessor.PixelThreshold}";
                RefreshPredictionFromCamera();
            };

            _holesLabel = new Label
            {
                AutoSize = true,
                Text = $"Заполнение дыр: {ImageProcessor.FillHolesMaxArea}",
                Location = new Point(_thresholdLabel.Left, _thresholdBar.Bottom + 10)
            };

            _holesBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 400,      // это ПЛОЩАДЬ дырки в пикселях 64×64
                Value = Math.Max(0, Math.Min(400, ImageProcessor.FillHolesMaxArea)),
                TickFrequency = 50,
                SmallChange = 1,
                LargeChange = 10,
                Width = _thresholdBar.Width,
                Location = new Point(_thresholdBar.Left, _holesLabel.Bottom + 4)
            };

            _holesBar.ValueChanged += (s, e) =>
            {
                ImageProcessor.FillHolesMaxArea = _holesBar.Value;
                _holesLabel.Text = $"Заполнение дыр: {ImageProcessor.FillHolesMaxArea}";
                RefreshPredictionFromCamera();
            };

            Controls.Add(_thresholdLabel);
            Controls.Add(_thresholdBar);
            Controls.Add(_holesLabel);
            Controls.Add(_holesBar);
        }

        private void RefreshPredictionFromCamera()
        {
            if (_isTraining) return;
            if (!autoRecognizeBox.Checked) return;
            if (cameraBox.Image == null) return;

            using (Bitmap frame = (Bitmap)cameraBox.Image.Clone())
                MakePrediction(frame);
        }

        private void InitCamera()
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count > 0)
                {
                    foreach (FilterInfo device in videoDevices)
                        cmbVideoSource.Items.Add(device.Name);
                    cmbVideoSource.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void btnToggleCamera_Click(object sender, EventArgs e)
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource = null;
            }
            else
            {
                if (videoDevices.Count == 0) return;
                videoSource = new VideoCaptureDevice(videoDevices[cmbVideoSource.SelectedIndex].MonikerString);

                VideoCapabilities bestRes = null;
                foreach (var cap in videoSource.VideoCapabilities)
                    if (bestRes == null || cap.FrameSize.Width > bestRes.FrameSize.Width)
                        bestRes = cap;

                if (bestRes != null) videoSource.VideoResolution = bestRes;

                videoSource.NewFrame += VideoSource_NewFrame;
                videoSource.Start();
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (_isTraining) return;

            try
            {
                Bitmap originalFrame = (Bitmap)eventArgs.Frame.Clone();
                originalFrame.RotateFlip(RotateFlipType.RotateNoneFlipX);

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_isTraining) return;

                        Image old = cameraBox.Image;
                        cameraBox.Image = originalFrame;
                        old?.Dispose();

                        if (autoRecognizeBox.Checked && (DateTime.Now - lastRecognitionTime > recognitionInterval))
                        {
                            lastRecognitionTime = DateTime.Now;
                            using (Bitmap forProcess = (Bitmap)cameraBox.Image.Clone())
                                MakePrediction(forProcess);
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void MakePrediction(Bitmap image)
        {
            if (_isTraining) return;

            double[] input = ImageProcessor.ProcessImage(image);

            if (debugPictureBox.Image != null) debugPictureBox.Image.Dispose();
            debugPictureBox.Image = ImageProcessor.GetProcessedBitmap(image);

            Sample sample = new Sample(input, (int)classCounter.Value, FigureType.Undef);
            Net.Predict(sample);

            StatusLabel.Text = "Вижу: " + sample.recognizedClass;
            label1.Text = "Распознано: " + sample.recognizedClass;
            label1.ForeColor = (sample.recognizedClass != FigureType.Undef) ? Color.Blue : Color.Black;

            label8.Text = string.Join("\n", sample.Output.Select(d => d.ToString("F2")));
            label9.Text = string.Join("\n", Enumerable.Range(0, (int)classCounter.Value).Select(i => i + ":"));
        }

        private void cameraBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (cameraBox.Image == null || _isTraining) return;
            using (Bitmap frame = (Bitmap)cameraBox.Image.Clone())
                MakePrediction(frame);
        }

        public void UpdateLearningInfo(double progress, double error, TimeSpan elapsedTime)
        {
            if ((DateTime.Now - _lastUiUpdate).TotalMilliseconds < 100 && progress < 0.99)
                return;

            _lastUiUpdate = DateTime.Now;

            if (progressBar1.InvokeRequired)
            {
                progressBar1.BeginInvoke(new TrainProgressHandler(UpdateLearningInfo), progress, error, elapsedTime);
                return;
            }

            StatusLabel.Text = "Ошибка: " + error.ToString("F5");
            int val = (int)(progress * 100);
            progressBar1.Value = Math.Max(0, Math.Min(100, val));
            elapsedTimeLabel.Text = "Время: " + elapsedTime.Duration().ToString(@"hh\:mm\:ss");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (_isTraining) return;
            foreach (var n in networksCache.Values) n.TrainProgress -= UpdateLearningInfo;
            networksCache = networksCache.ToDictionary(oldNet => oldNet.Key, oldNet => CreateNetwork(oldNet.Key));
        }

        private int[] CurrentNetworkStructure()
        {
            return netStructureBox.Text.Split(';').Select(int.Parse).ToArray();
        }

        private void classCounter_ValueChanged(object sender, EventArgs e)
        {
            generator.FigureCount = (int)classCounter.Value;
            var vals = netStructureBox.Text.Split(';');
            if (vals.Length > 0)
            {
                vals[vals.Length - 1] = classCounter.Value.ToString();
                netStructureBox.Text = string.Join(";", vals);
            }
        }

        private BaseNetwork CreateNetwork(string networkName)
        {
            var network = networksFabric[networkName](CurrentNetworkStructure());
            network.TrainProgress += UpdateLearningInfo;
            return network;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                System.Threading.Thread.Sleep(200);
            }
            base.OnFormClosing(e);
        }

        private async Task<double> train_networkAsync(int training_size, int epoches, double acceptable_error, bool parallel = true)
        {
            if (generator.CountLoadedTemplates() == 0)
            {
                MessageBox.Show("Нет картинок в папке Dataset!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
            if (_isTraining) return 0;

            _isTraining = true;
            label1.Text = "Обучение...";
            groupBox1.Enabled = false;

            _thresholdBar.Enabled = false;
            _holesBar.Enabled = false;

            if (videoSource != null) videoSource.NewFrame -= VideoSource_NewFrame;

            SamplesSet samples = new SamplesSet();
            for (int i = 0; i < training_size; i++)
                samples.AddSample(generator.GenerateFigure());

            try
            {
                var curNet = Net;
                double f = await Task.Run(() => curNet.TrainOnDataSet(samples, epoches, acceptable_error, parallel));
                label1.Text = "Готово!";
                return f;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
                return 0;
            }
            finally
            {
                _isTraining = false;
                groupBox1.Enabled = true;

                _thresholdBar.Enabled = true;
                _holesBar.Enabled = true;

                if (videoSource != null) videoSource.NewFrame += VideoSource_NewFrame;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
#pragma warning disable CS4014
            train_networkAsync((int)TrainingSizeCounter.Value, (int)EpochesCounter.Value,
                (100 - AccuracyCounter.Value) / 100.0, parallelCheckBox.Checked);
#pragma warning restore CS4014
        }

        private void btnTrainOne_Click(object sender, EventArgs e)
        {
            if (Net == null || _isTraining) return;
            Sample fig = generator.GenerateFigure();
            pictureBox1.Image = generator.GenBitmap();
            Net.Train(fig, 0.00005, parallelCheckBox.Checked);
            label1.Text = "Распознано: " + fig.recognizedClass;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (_isTraining) return;
            Enabled = false;

            SamplesSet samples = new SamplesSet();
            for (int i = 0; i < (int)TrainingSizeCounter.Value; i++)
                samples.AddSample(generator.GenerateFigure());

            double accuracy = samples.TestNeuralNetwork(Net);
            StatusLabel.Text = $"Точность: {accuracy * 100:F2}%";
            Enabled = true;
        }

        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (_isTraining) return;

            Sample fig = generator.GenerateFigure();
            Net.Predict(fig);
            label1.Text = "Распознано: " + fig.recognizedClass;
            pictureBox1.Image = generator.GenBitmap();

            label8.Text = string.Join("\n", fig.Output.Select(d => d.ToString("F2")));
            label9.Text = string.Join("\n", Enumerable.Range(0, (int)classCounter.Value).Select(i => i + ":"));
        }

        private void recreateNetButton_MouseEnter(object sender, EventArgs e) { }
        private void netTrainButton_MouseEnter(object sender, EventArgs e) { }
        private void testNetButton_MouseEnter(object sender, EventArgs e) { }
    }
}
