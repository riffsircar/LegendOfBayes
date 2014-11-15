﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MicrosoftResearch.Infer;
using MicrosoftResearch.Infer.Collections;
using MicrosoftResearch.Infer.Distributions;
using MicrosoftResearch.Infer.Factors;
using MicrosoftResearch.Infer.Graphs;
using MicrosoftResearch.Infer.Models;
using MicrosoftResearch.Infer.Maths;
using MicrosoftResearch.Infer.Transforms;
using MicrosoftResearch.Infer.Utils;
using MicrosoftResearch.Infer.Views;
namespace ZeldaInfer
{
    class Program
    {
        #region Tutorials
        protected static void Tutorial1()
        {
            Variable<bool> coin1 = Variable.Bernoulli(0.5);
            Variable<bool> coin2 = Variable.Bernoulli(0.5);
            Variable<bool> bothHeads = coin1 & coin2;
            InferenceEngine ie = new InferenceEngine();
            Console.WriteLine("Probability both coins are heads: " + ie.Infer(bothHeads));
            bothHeads.ObservedValue = false;
            Console.WriteLine("Probability distribution over firstCoin: " + ie.Infer(coin1));

        }
        protected static void Tutorial2()
        {
            Variable<double> threshold = Variable.New<double>().Named("threshold");
            Variable<double> x = Variable.GaussianFromMeanAndVariance(0, 1).Named("x");
            Variable.ConstrainTrue(x > threshold);
            InferenceEngine engine = new InferenceEngine();
            engine.Algorithm = new ExpectationPropagation();
            for (double thresh = 0; thresh <= 1; thresh += 0.1)
            {
                threshold.ObservedValue = thresh;
                Console.WriteLine("Dist over x given thresh of " + thresh + "=" + engine.Infer(x));
            }
           
        }
        protected static void Tutorial3()
        {
            double[] data = new double[1000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Rand.Normal(0, 1);
            }
            Variable<double> mean = Variable.GaussianFromMeanAndVariance(0, 100);
            Variable<double> precision = Variable.GammaFromShapeAndScale(1, 1);
            Range dataRange = new Range(data.Length).Named("n");
            VariableArray<double> x = Variable.Array<double>(dataRange);
            x[dataRange] = Variable.GaussianFromMeanAndPrecision(mean, precision).ForEach(dataRange);
            x.ObservedValue = data;
            InferenceEngine engine = new InferenceEngine();
            //engine.ShowFactorGraph = true;
            // Retrieve the posterior distributions
            Console.WriteLine("mean=" + engine.Infer(mean));
            Console.WriteLine("prec=" + engine.Infer(precision));

        }
        static void Tutorial4()
        {
            double[] incomes = { 63, 16, 28, 55, 22, 20 };
            double[] ages = { 38, 23, 40, 27, 18, 40 };
            bool[] willBuy = { true, false, true, true, false, false };

            // Create x vector, augmented by 1
            Vector[] xdata = new Vector[incomes.Length];
            for (int i = 0; i < xdata.Length; i++)
                xdata[i] = Vector.FromArray(incomes[i], ages[i], 1);
            VariableArray<Vector> x = Variable.Observed(xdata);

            // Create target y
            VariableArray<bool> y = Variable.Observed(willBuy, x.Range);
            Variable<Vector> w = Variable.Random(
                new VectorGaussian(Vector.Zero(3), PositiveDefiniteMatrix.Identity(3)));
            Range j = y.Range;
            double noise = 0.1;
            y[j] = Variable.GaussianFromMeanAndVariance(Variable.InnerProduct(w, x[j]), noise) > 0;

            InferenceEngine engine = new InferenceEngine(new ExpectationPropagation());
            VectorGaussian wPosterior = engine.Infer<VectorGaussian>(w);
            Console.WriteLine("Dist over w=\n" + wPosterior); 
            
            double[] incomesTest = { 58, 18, 22 };
            double[] agesTest = { 36, 24, 37 };
            VariableArray<bool> ytest = Variable.Array<bool>(new Range(agesTest.Length));
            BayesPointMachine(incomesTest, agesTest, Variable.Random(wPosterior), ytest);
            Console.WriteLine("output=\n" + engine.Infer(ytest));
        }

        public static void BayesPointMachine(
            double[] incomes,
            double[] ages,
            Variable<Vector> w,
            VariableArray<bool> y)
        {
            // Create x vector, augmented by 1
            Range j = y.Range;
            Vector[] xdata = new Vector[incomes.Length];
            for (int i = 0; i < xdata.Length; i++)
                xdata[i] = Vector.FromArray(incomes[i], ages[i], 1);
            VariableArray<Vector> x = Variable.Observed(xdata, j);

            // Bayes Point Machine
            double noise = 0.1;
            y[j] = Variable.GaussianFromMeanAndVariance(Variable.InnerProduct(w, x[j]), noise) > 0;
        }
        static void Tutorial5()
        {

            // Data from clinical trial
            VariableArray<bool> controlGroup =
              Variable.Observed(new bool[] { false, false, true, false, false });
            VariableArray<bool> treatedGroup =
              Variable.Observed(new bool[] { true, false, true, true, true });
            Range i = controlGroup.Range; Range j = treatedGroup.Range;
            // Prior on being effective treatment
            Variable<bool> isEffective = Variable.Bernoulli(0.5);
            Variable<double> probIfTreated, probIfControl;
            using (Variable.If(isEffective))
            {
                // Model if treatment is effective
                probIfControl = Variable.Beta(1, 1);
                controlGroup[i] = Variable.Bernoulli(probIfControl).ForEach(i);
                probIfTreated = Variable.Beta(1, 1);
                treatedGroup[j] = Variable.Bernoulli(probIfTreated).ForEach(j);
            }
            using (Variable.IfNot(isEffective))
            {
                // Model if treatment is not effective
                Variable<double> probAll = Variable.Beta(1, 1);
                controlGroup[i] = Variable.Bernoulli(probAll).ForEach(i);
                treatedGroup[j] = Variable.Bernoulli(probAll).ForEach(j);
            } 
            InferenceEngine ie = new InferenceEngine();
            Console.WriteLine("Probability treatment has an effect = " + ie.Infer(isEffective));
            Console.WriteLine("Probability of good outcome if given treatment = "
                               + (float)ie.Infer<Beta>(probIfTreated).GetMean());
            Console.WriteLine("Probability of good outcome if control = "
                               + (float)ie.Infer<Beta>(probIfControl).GetMean());
        }
        static void Tutorial6()
        {
            Range k = new Range(2);
            VariableArray<Vector> means = Variable.Array<Vector>(k);
            means[k] = Variable.VectorGaussianFromMeanAndPrecision(
              Vector.FromArray(0.0, 0.0), PositiveDefiniteMatrix.IdentityScaledBy(2, 0.01)).ForEach(k);

            VariableArray<PositiveDefiniteMatrix> precs = Variable.Array<PositiveDefiniteMatrix>(k);
            precs[k] = Variable.WishartFromShapeAndScale(
              100.0, PositiveDefiniteMatrix.IdentityScaledBy(2, 0.01)).ForEach(k);

            Variable<Vector> weights = Variable.Dirichlet(k, new double[] { 1, 1 });

            Range n = new Range(300);
            VariableArray<Vector> data = Variable.Array<Vector>(n);

            VariableArray<int> z = Variable.Array<int>(n);

            using (Variable.ForEach(n))
            {
                z[n] = Variable.Discrete(weights);
                using (Variable.Switch(z[n]))
                {
                    data[n] = Variable.VectorGaussianFromMeanAndPrecision(
                      means[z[n]], precs[z[n]]);
                }
            }
            data.ObservedValue = GenerateData(n.SizeAsInt); 
            // The inference
            Discrete[] zinit = new Discrete[n.SizeAsInt]; 
            for (int i = 0; i < zinit.Length; i++)
                zinit[i] = Discrete.PointMass(Rand.Int(k.SizeAsInt), k.SizeAsInt);
            z.InitialiseTo(Distribution<int>.Array(zinit));
            InferenceEngine ie = new InferenceEngine(new VariationalMessagePassing());
            Console.WriteLine("Dist over pi=" + ie.Infer(weights));
            Console.WriteLine("Dist over means=\n" + ie.Infer(means));
            Console.WriteLine("Dist over precs=\n" + ie.Infer(precs));
        }
        public static Vector[] GenerateData(int nData)
        {
            Vector trueM1 = Vector.FromArray(2.0, 3.0);
            Vector trueM2 = Vector.FromArray(7.0, 5.0);
            PositiveDefiniteMatrix trueP1 = new PositiveDefiniteMatrix(
                new double[,] { { 3.0, 0.2 }, { 0.2, 2.0 } });
            PositiveDefiniteMatrix trueP2 = new PositiveDefiniteMatrix(
                new double[,] { { 2.0, 0.4 }, { 0.4, 4.0 } });
            VectorGaussian trueVG1 = VectorGaussian.FromMeanAndPrecision(trueM1, trueP1);
            VectorGaussian trueVG2 = VectorGaussian.FromMeanAndPrecision(trueM2, trueP2);
            double truePi = 0.6;
            Bernoulli trueB = new Bernoulli(truePi);
            // Restart the infer.NET random number generator
            Rand.Restart(12347);
            Vector[] data = new Vector[nData];
            for (int j = 0; j < nData; j++)
            {
                bool bSamp = trueB.Sample();
                data[j] = bSamp ? trueVG1.Sample() : trueVG2.Sample();
            }
            return data;
        }
        public static void Tutorial7(){
            int length = 2;

            double[] xProbs = { 0.4, 0.6 };
            Variable<int> x = Variable.Discrete(xProbs).Named("x");
            double[] yProbs = { 0.7, 0.3 };
            Variable<int> y = Variable.Discrete(yProbs).Named("y");
            double[] zProbs = { 0.8, 0.2 };
            Variable<int> z = Variable.Discrete(zProbs).Named("z"); 

            Variable<int> xy = Variable.Discrete(.25, 0.125, 0.125, .25);
            Variable<int> yz = Variable.Discrete(.25, 0.125, 0.125, .25);
            Variable<int> zx = Variable.Discrete(.25, 0.125, 0.125, .25);

            int pair_index = 0;

            for (int i = 0; i < length; i++) {
                for (int j = 0; j < length; j++, pair_index++)   {
                    using (Variable.Case(xy, pair_index))  {
                        Variable.ConstrainEqual(x, i);
                        Variable.ConstrainEqual(y, j);
                    }

 

                    using (Variable.Case(yz, pair_index)) {
                        Variable.ConstrainEqual(y, i);
                        Variable.ConstrainEqual(z, j);
                    } 

                    using (Variable.Case(zx, pair_index)) {
                        Variable.ConstrainEqual(z, i);
                        Variable.ConstrainEqual(x, j);
                    }
                }
            }

 

            InferenceEngine engine = new InferenceEngine(new ExpectationPropagation());

 

            // Retrieve the posterior distributions

            Console.WriteLine("x = " + engine.Infer(x));
            Console.WriteLine("y = " + engine.Infer(y));
            Console.WriteLine("z = " + engine.Infer(z));


        }
        #endregion

        static void Sprinkler()
        {
            bool[] rainData = new bool[1000];
            bool[] sprinklerData = new bool[1000];
            bool[] grassData = new bool[1000];
            int totalGrassWet = 0;
            for (int i = 0; i < rainData.Length; i++)
            {
                rainData[i] = Rand.Double() < 0.1;
                if (rainData[i])
                {
                    sprinklerData[i] = Rand.Double() < 0.1;
                }
                else
                {
                    sprinklerData[i] = Rand.Double() < 0.4;
                }

                if (rainData[i] || sprinklerData[i])
                {
                    totalGrassWet++;
                    grassData[i] = Rand.Double() < 0.99;
                }
                else
                {
                    grassData[i] = Rand.Double() < 0.01;

                }
            }
            Console.WriteLine("GrassWet " + totalGrassWet);
            Range dataRange = new Range(grassData.Length).Named("n");

            Variable<double> rainRate = Variable.Beta(8, 1).Named("rainRate");
            VariableArray<bool> rain = Variable.Array<bool>(dataRange).Named("rain");
            rain[dataRange] = Variable.Bernoulli(rainRate).ForEach(dataRange);

            Variable<double> sprinklerNoRain = Variable.Beta(7, 1).Named("sprinklerNoRain");
            Variable<double> sprinklerRain = Variable.Beta(6, 1).Named("sprinklerRain");
            VariableArray<bool> sprinkler = Variable.Array<bool>(dataRange).Named("sprinkler");
          
            using (Variable.ForEach(dataRange))
            {
                using (Variable.If(rain[dataRange]))
                {
                    sprinkler[dataRange] = Variable.Bernoulli(sprinklerRain);
                }
                using (Variable.IfNot(rain[dataRange]))
                {
                    sprinkler[dataRange] = Variable.Bernoulli(sprinklerNoRain);
                }
            }

            Variable<double> grassRainSprinkler = Variable.Beta(2, 1).Named("grassRainSprinkler");
            Variable<double> grassNoRainSprinkler = Variable.Beta(3, 1).Named("grassNoRainSprinkler");
            Variable<double> grassRainNoSprinkler = Variable.Beta(4, 1).Named("grassRainNoSprinkler");
            Variable<double> grassNoRainNoSprinkler = Variable.Beta(5, 1).Named("grassNoRainNoSprinkler");
            VariableArray<bool> grass = Variable.Array<bool>(dataRange).Named("grass");
            using (Variable.ForEach(dataRange))
            {
                using (Variable.If(rain[dataRange]))
                {
                    using (Variable.If(sprinkler[dataRange]))
                    {
                        grass[dataRange] = Variable.Bernoulli(grassRainSprinkler);
                    }
                    using (Variable.IfNot(sprinkler[dataRange]))
                    {
                        grass[dataRange] = Variable.Bernoulli(grassRainNoSprinkler);

                    }
                }
                using (Variable.IfNot(rain[dataRange]))
                {
                    using (Variable.If(sprinkler[dataRange]))
                    {
                        grass[dataRange] = Variable.Bernoulli(grassNoRainSprinkler);

                    }
                    using (Variable.IfNot(sprinkler[dataRange]))
                    {
                        grass[dataRange] = Variable.Bernoulli(grassNoRainNoSprinkler);
                    }
                }
            }
            InferenceEngine engine = new InferenceEngine();

            grass.ObservedValue = grassData;
            sprinkler.ObservedValue = sprinklerData;
            rain.ObservedValue = rainData;
            Console.WriteLine(engine.Infer(rainRate));
            Console.WriteLine(engine.Infer(sprinklerNoRain));
            Console.WriteLine(engine.Infer(sprinklerRain));
            Console.WriteLine(engine.Infer(grassRainSprinkler));
            Console.WriteLine(engine.Infer(grassRainNoSprinkler));
            Console.WriteLine(engine.Infer(grassNoRainSprinkler));
            Console.WriteLine(engine.Infer(grassNoRainNoSprinkler));
        }

        static void Main(string[] args)
        {
            Sprinkler();
            Console.Read(); 
        }
    }
}