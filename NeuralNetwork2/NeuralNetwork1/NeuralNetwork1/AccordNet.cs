using System.Diagnostics;
using System.IO;
using System.Linq;
using Accord.Neuro;
using Accord.Neuro.Learning;

namespace NeuralNetwork1
{
    class AccordNet : BaseNetwork
    {
        private ActivationNetwork network;
        public Stopwatch stopWatch = new Stopwatch();

        public AccordNet(int[] structure)
        {
            network = new ActivationNetwork(new SigmoidFunction(2.0), structure[0], structure.Skip(1).ToArray());
            new NguyenWidrow(network).Randomize();
        }

        public override int Train(Sample sample, double acceptableError, bool parallel)
        {
            var teacher = MakeTeacher(parallel);
            int iters = 1;
            while (teacher.Run(sample.input, sample.Output) > acceptableError)
            {
                ++iters;
            }
            return iters;
        }

        private ISupervisedLearning MakeTeacher(bool parallel)
        {
            if (parallel)
                return new ParallelResilientBackpropagationLearning(network);
            return new ResilientBackpropagationLearning(network);
        }

        public override double TrainOnDataSet(SamplesSet samplesSet, int epochsCount, double acceptableError, bool parallel)
        {
            // Подготовка данных
            double[][] inputs = new double[samplesSet.Count][];
            double[][] outputs = new double[samplesSet.Count][];

            for (int i = 0; i < samplesSet.Count; ++i)
            {
                inputs[i] = samplesSet[i].input;
                outputs[i] = samplesSet[i].Output;
            }

            int epoch_to_run = 0;
            var teacher = MakeTeacher(parallel);
            double error = double.PositiveInfinity;

#if DEBUG
            // StreamWriter errorsFile = File.CreateText("errors.csv"); // Можно раскомментировать для логов
#endif

            stopWatch.Restart();
            long lastUpdateMs = 0; // Для троттлинга UI

            while (epoch_to_run < epochsCount && error > acceptableError)
            {
                epoch_to_run++;
                error = teacher.RunEpoch(inputs, outputs);

#if DEBUG
                // errorsFile.WriteLine(error);
#endif

                // ИСПРАВЛЕНИЕ: Обновляем UI не чаще 5 раз в секунду (раз в 200мс)
                // Иначе на быстрых сетях поток UI захлебнется
                if (stopWatch.ElapsedMilliseconds - lastUpdateMs > 200 || epoch_to_run == epochsCount)
                {
                    OnTrainProgress((epoch_to_run * 1.0) / epochsCount, error, stopWatch.Elapsed);
                    lastUpdateMs = stopWatch.ElapsedMilliseconds;
                }
            }

#if DEBUG
            // errorsFile.Close();
#endif
            stopWatch.Stop();
            OnTrainProgress(1.0, error, stopWatch.Elapsed); // Финальное обновление

            return error;
        }

        protected override double[] Compute(double[] input)
        {
            return network.Compute(input);
        }
    }
}