using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NeuralNetwork1
{
    /// <summary>
    /// "Студенческий" многослойный персептрон (полносвязная сеть) с сигмоидой и обратным распространением ошибки.
    /// Поддерживает произвольное число слоёв, задаваемое массивом structure:
    ///   structure[0] - число входов (сенсоров), далее размеры скрытых слоёв, последний - число классов.
    /// </summary>
    public class StudentNetwork : BaseNetwork
    {
        private readonly int[] _structure;           // включая входной слой
        private readonly int _layersCount;           // число вычисляемых слоёв = structure.Length - 1 (скрытые + выходной)

        // Нейроны (активации) по слоям (без входного): _neurons[l][j]
        private readonly double[][] _neurons;

        // Смещения (bias) по слоям: _biases[l][j]
        private readonly double[][] _biases;

        // Веса: _weights[l][j][i] - вес от i-го нейрона предыдущего слоя к j-му нейрону текущего слоя l.
        // Для l=0 предыдущий слой - вход (сенсоры).
        private readonly double[][][] _weights;

        // Дельты (градиенты по нейронам) для backprop: _deltas[l][j]
        private readonly double[][] _deltas;

        // Нормировка входов: каждый сенсор - количество "чёрных" пикселей в строке/столбце (максимум 200).
        // Чтобы сигмоида не уходила в насыщение, делим на 200.
        private const double InputScale = 1.0 / 200.0;

        // Скорость обучения (learning rate) — шаг градиентного спуска.
        // Значение подобрано под нормированные входы; при желании можно тюнить.
        private const double LearningRate = 0.25;

        // Ограничение итераций при обучении одному образу, чтобы не зависнуть на "трудном" примере.
        private const int MaxItersPerSample = 10_000;

        private readonly Random _rand = new Random();

        // Секундомер для отображения времени обучения в форме (как в AccordNet).
        private readonly Stopwatch _stopWatch = new Stopwatch();

        public StudentNetwork(int[] structure)
        {
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            if (structure.Length < 2) throw new ArgumentException("Сеть должна иметь минимум 2 слоя (вход и выход).", nameof(structure));
            if (structure[0] <= 0) throw new ArgumentException("Размер входного слоя должен быть положительным.", nameof(structure));
            if (structure.Any(s => s <= 0)) throw new ArgumentException("Все размеры слоёв должны быть положительными.", nameof(structure));

            _structure = (int[])structure.Clone();
            _layersCount = _structure.Length - 1;

            _neurons = new double[_layersCount][];
            _biases = new double[_layersCount][];
            _deltas = new double[_layersCount][];
            _weights = new double[_layersCount][][];

            // Инициализация слоёв, смещений и весов.
            for (int l = 0; l < _layersCount; l++)
            {
                int curSize = _structure[l + 1];
                int prevSize = _structure[l];

                _neurons[l] = new double[curSize];
                _biases[l] = new double[curSize];
                _deltas[l] = new double[curSize];

                _weights[l] = new double[curSize][];
                for (int j = 0; j < curSize; j++)
                    _weights[l][j] = new double[prevSize];

                InitLayerWeightsXavier(l, prevSize, curSize);
            }
        }

        /// <summary>
        /// Инициализация весов по "Xavier/Glorot" (равномерное распределение).
        /// Это ускоряет сходимость и снижает риск насыщения активаций на старте.
        /// </summary>
        private void InitLayerWeightsXavier(int layerIndex, int fanIn, int fanOut)
        {
            // limit = sqrt(6 / (fanIn + fanOut))
            double limit = Math.Sqrt(6.0 / (fanIn + fanOut));

            for (int j = 0; j < fanOut; j++)
            {
                _biases[layerIndex][j] = NextUniform(-limit, limit);

                var w = _weights[layerIndex][j];
                for (int i = 0; i < fanIn; i++)
                    w[i] = NextUniform(-limit, limit);
            }
        }

        private double NextUniform(double min, double max)
        {
            return min + _rand.NextDouble() * (max - min);
        }

        private static double Sigmoid(double x)
        {
            // лёгкая защита от переполнения exp при больших |x|
            if (x < -60) return 0.0;
            if (x > 60) return 1.0;
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        private static double SigmoidDerivativeFromOutput(double y)
        {
            // y = sigmoid(x) => y' = y*(1-y)
            return y * (1.0 - y);
        }

        /// <summary>
        /// Прямой проход (forward) с сохранением активаций в _neurons.
        /// </summary>
        private double[] Forward(double[] input, bool parallel)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.Length != _structure[0])
                throw new ArgumentException($"Ожидался вход длины {_structure[0]}, получено {input.Length}.");

            // prevA - выходы предыдущего слоя; для l=0 это вход.
            // Чтобы не копировать массив, для l=0 читаем input напрямую, просто домножая на InputScale.
            for (int l = 0; l < _layersCount; l++)
            {
                int curSize = _structure[l + 1];
                int prevSize = _structure[l];

                Action<int> computeNeuron = j =>
                {
                    double sum = _biases[l][j];

                    var w = _weights[l][j];

                    if (l == 0)
                    {
                        // Входной слой
                        for (int i = 0; i < prevSize; i++)
                            sum += w[i] * (input[i] * InputScale);
                    }
                    else
                    {
                        // Предыдущий слой - уже активации
                        var prev = _neurons[l - 1];
                        for (int i = 0; i < prevSize; i++)
                            sum += w[i] * prev[i];
                    }

                    _neurons[l][j] = Sigmoid(sum);
                };

                if (parallel)
                    Parallel.For(0, curSize, computeNeuron);
                else
                    for (int j = 0; j < curSize; j++) computeNeuron(j);
            }

            return _neurons[_layersCount - 1];
        }

        /// <summary>
        /// Обратный проход (backprop) и обновление весов по одному образу.
        /// </summary>
        private void BackwardAndUpdate(double[] input, double[] target, bool parallel)
        {
            int outLayer = _layersCount - 1;
            int outSize = _structure[_structure.Length - 1];

            // 1) Дельты выходного слоя.
            //
            // Для многоклассовой классификации с сигмоидой на выходе MSE часто учится медленно.
            // Ускорение: используем "кросс-энтропию + сигмоида". В этом случае градиент по сумматору выходного нейрона равен (y - t),
            // без дополнительного множителя sigmoid'(y) — это уменьшает затухание градиента на выходе.
            {
                var y = _neurons[outLayer];
                var d = _deltas[outLayer];
                for (int j = 0; j < outSize; j++)
                {
                    d[j] = (y[j] - target[j]);
                }
            }

            // 2) Дельты скрытых слоёв: (W^T * delta_next) * sigmoid'(y)
            for (int l = outLayer - 1; l >= 0; l--)
            {
                int curSize = _structure[l + 1];
                int nextSize = _structure[l + 2];

                var curY = _neurons[l];
                var curD = _deltas[l];
                var nextD = _deltas[l + 1];
                var nextW = _weights[l + 1]; // nextW[k][j] - вес от j в текущем слое к k в следующем

                Action<int> computeDelta = j =>
                {
                    double sum = 0.0;
                    for (int k = 0; k < nextSize; k++)
                        sum += nextD[k] * nextW[k][j];

                    curD[j] = sum * SigmoidDerivativeFromOutput(curY[j]);
                };

                if (parallel)
                    Parallel.For(0, curSize, computeDelta);
                else
                    for (int j = 0; j < curSize; j++) computeDelta(j);
            }

            // 3) Обновление весов и смещений
            for (int l = 0; l < _layersCount; l++)
            {
                int curSize = _structure[l + 1];
                int prevSize = _structure[l];

                // prevA: входы для этого слоя
                // для l==0 это input (нормированный), для остальных это _neurons[l-1].
                Action<int> updateNeuron = j =>
                {
                    double delta = _deltas[l][j];
                    _biases[l][j] -= LearningRate * delta;

                    var w = _weights[l][j];

                    if (l == 0)
                    {
                        for (int i = 0; i < prevSize; i++)
                            w[i] -= LearningRate * delta * (input[i] * InputScale);
                    }
                    else
                    {
                        var prev = _neurons[l - 1];
                        for (int i = 0; i < prevSize; i++)
                            w[i] -= LearningRate * delta * prev[i];
                    }
                };

                if (parallel)
                    Parallel.For(0, curSize, updateNeuron);
                else
                    for (int j = 0; j < curSize; j++) updateNeuron(j);
            }
        }

        private double ComputeMse(double[] output, double[] target)
        {
            // MSE = mean((y - t)^2) — удобно тем, что лежит в диапазоне 0..1 для one-hot целей.
            double sum = 0.0;
            for (int i = 0; i < output.Length; i++)
            {
                double d = output[i] - target[i];
                sum += d * d;
            }
            return sum / output.Length;
        }

        public override int Train(Sample sample, double acceptableError, bool parallel)
        {
            if (sample == null) throw new ArgumentNullException(nameof(sample));

            // Целевой вектор (one-hot) надо сохранить, потому что ProcessPrediction внутри Sample перезапишет Output.
            double[] target = BuildOneHotTarget(_structure[_structure.Length - 1], (int)sample.actualClass);

            int iters = 0;
            double mse = double.PositiveInfinity;

            while (iters < MaxItersPerSample && mse > acceptableError)
            {
                var output = Forward(sample.input, parallel);
                mse = ComputeMse(output, target);

                if (mse <= acceptableError)
                    break;

                BackwardAndUpdate(sample.input, target, parallel);
                iters++;
            }

            // Обновим поля Sample так, чтобы форма могла показать "что сеть думает" после обучения.
            sample.ProcessPrediction((double[])_neurons[_layersCount - 1].Clone());

            return iters;
        }

        public override double TrainOnDataSet(SamplesSet samplesSet, int epochsCount, double acceptableError, bool parallel)
        {
            if (samplesSet == null) throw new ArgumentNullException(nameof(samplesSet));
            if (epochsCount <= 0) throw new ArgumentOutOfRangeException(nameof(epochsCount));

            _stopWatch.Restart();

            // Индексы для перемешивания (stochastic gradient descent).
            int n = samplesSet.Count;
            int[] idx = Enumerable.Range(0, n).ToArray();

            double epochMse = double.PositiveInfinity;

            for (int epoch = 1; epoch <= epochsCount; epoch++)
            {
                Shuffle(idx);

                double sumMse = 0.0;

                for (int s = 0; s < n; s++)
                {
                    var sample = samplesSet[idx[s]];
                    double[] target = BuildOneHotTarget(_structure[_structure.Length - 1], (int)sample.actualClass);

                    var output = Forward(sample.input, parallel);
                    sumMse += ComputeMse(output, target);

                    BackwardAndUpdate(sample.input, target, parallel);
                }

                epochMse = sumMse / n;

                OnTrainProgress(epoch * 1.0 / epochsCount, epochMse, _stopWatch.Elapsed);

                if (epochMse <= acceptableError)
                    break;
            }

            OnTrainProgress(1.0, epochMse, _stopWatch.Elapsed);
            _stopWatch.Stop();

            return epochMse;
        }

        private void Shuffle(int[] a)
        {
            // Fisher–Yates
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = _rand.Next(i + 1);
                int tmp = a[i];
                a[i] = a[j];
                a[j] = tmp;
            }
        }

        protected override double[] Compute(double[] input)
        {
            // Для распознавания (Predict) используем прямой проход.
            // Здесь не нужно распараллеливание — Predict вызывается из UI по клику.
            // Но чтобы не ломать контракт, оставляем последовательный вариант.
            var output = Forward(input, parallel: false);
            return (double[])output.Clone();
        }


        private static double[] BuildOneHotTarget(int classesCount, int classIndex)
        {
            var t = new double[classesCount];
            if (classIndex >= 0 && classIndex < classesCount) t[classIndex] = 1.0;
            return t;
        }
    }
}