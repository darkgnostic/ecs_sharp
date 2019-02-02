using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECS
{
    public class COM1 : Component { public COM1() { mFamilyId = 5; } public int Value = 0; }
    public class COM2 : Component { public COM2() { mFamilyId = 2; } public int Value = 0; }
    public class COM3 : Component { public COM3() { mFamilyId = 3; } public int Value = 0; }
    public class COM4 : Component { public COM4() { mFamilyId = 4; } public int Value = 0; }

    class ComponentSystemTester : ComponentSystem
    {
        private void CreateComponents( int numEntities, int numComponents )
        {
            Clear();
            for (int i = 0; i <= numEntities; i++)
                Entity.CreateNewEntity();

            for (int i = 0; i < numComponents; i++)
            {
                CreateComponent<COM1>(RND.GetRandomNumber(1, numEntities));
                CreateComponent<COM2>(RND.GetRandomNumber(1, numEntities));
                CreateComponent<COM3>(RND.GetRandomNumber(1, numEntities));
                CreateComponent<COM4>(RND.GetRandomNumber(1, numEntities));
            }
        }
        public void Test()
        {
            int e1 = Entity.CreateNewEntity();

            var c1_1 = CreateComponent<COM1>(e1);
            var c2_1 = CreateComponent<COM2>(e1);
            var c3_1 = CreateComponent<COM3>(e1);

            int e2 = Entity.CreateNewEntity();

            var c1_2 = CreateComponent<COM1>(e2);
            var c2_2 = CreateComponent<COM2>(e2);
            var c3_2 = CreateComponent<COM3>(e2);

            COM4 com4 = new COM4
            {
                mEntityId = e1
            };

            AttachComponent(com4);

            DuplicateComponent<COM4>(e2, ref com4);


            Benchmark();
        }

        public void Benchmark()
        {
            CreateComponents(100, 100);
            Get<COM4>(0, -1);   // should produce null

            int numEntites = 1000;
            int numComponents = 100000;
            Benchmark(() =>
            {
                /* your code */
                CreateComponents(numEntites, numComponents/4);
            }, 10, "Creating components");


            Console.Write("Component count is: {0}, ", Size - ErasedIDSize);
            Benchmark(() =>
            {
                /* your code */
                for (int i = 0; i < 100000; i++)
                {
                    List<Component> comList = new List<Component>();
                    GetComponentsByEntity(RND.GetRandomNumber(1, numEntites), ref comList);
                }
            }, 100, "Fetching 100K times by entity");

            Console.Write("Component count is: {0}, ", Size - ErasedIDSize);
            Benchmark(() =>
            {
                /* your code */
                for (int i = 0; i < 100000; i++)
                {
                    List<Component> comList = new List<Component>();
                    GetComponentsByEntityAndFamily(RND.GetRandomNumber(1, numEntites), RND.GetRandomNumber(1, 4), ref comList);
                }
            }, 10, "Fetching 100K times by Entity & Family");

            Console.Write("Component count is: {0}, ", Size - ErasedIDSize);
            Benchmark(() =>
            {
                /* your code */
                for (int i = 0; i < 100000; i++)
                {
                    List<Component> comList = new List<Component>();
                    GetComponentsByFamilyAndEntity(RND.GetRandomNumber(1, 4), RND.GetRandomNumber(1, numEntites), ref comList);
                }
            }, 10, "Fetching 100K times by Family & Entity");

            Console.Write("Component count is: {0}, ", Size - ErasedIDSize);
            Benchmark(() =>
            {
                /* your code */
                for (int i = 0; i < 100000; i++)
                {
                    List<Component> comList = new List<Component>();
                    GetComponentsByEntity(RND.GetRandomNumber(1, numEntites), ref comList);
                }
            }, 10, "Fetching 100K times by Family");

            
            Console.Write("Component count is: {0}, ", Size - ErasedIDSize);
            Benchmark(() =>
            {
                CreateComponents(numEntites, 10000);
                for (int i = 0; i < 100000; i++)
                {
                    var uid = RND.GetRandomNumber(0, Size);
                    DeleteComponent(uid);
                }
                Validate();
            }, 10, "Deleting 1K components");

            
            Console.Write("Count after deletion is: {0}\n", Size - ErasedIDSize);
        }

        private static void Benchmark(Action act, int iterations, string desc)
        {
            Console.Write(desc);
            GC.Collect();
            int x = Console.CursorLeft;
            int y = Console.CursorTop;
            //act.Invoke(); // run once outside of loop to avoid initialization costs
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 1; i <= iterations; i++)
            {
                Console.SetCursorPosition(x, y);
                Console.Write(" (" + i + " iterations,");
                act.Invoke();
                Console.Write("avg: " + (sw.ElapsedMilliseconds / i).ToString() + "ms, total: " + sw.ElapsedMilliseconds + "ms)    ");
            }
            sw.Stop();
            Console.Write("\n");
        }
    }
}
