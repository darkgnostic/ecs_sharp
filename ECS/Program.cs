using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using ECS.Component;

namespace ECS
{
    class RND
    {
        private static readonly Random getrandom = new Random();

        public static int GetRandomNumber(int min, int max)
        {
            lock (getrandom) // synchronize
            {
                return getrandom.Next(min, max);
            }
        }
    }

    class CFID
    {
        public const int HEALTH = 1;
        public const int ARMOR = 2;
        public const int ATTACK = 3;
        public const int NAME = 4;
    }

    // component holding entity's name
    class Name : Component {
	    public Name() { name = ""; mFamilyId = CFID.NAME; }
        public string name;
    };

    // component holding entity's health
    class Health : Component {
        public Health() { health = 10;  mFamilyId = CFID.HEALTH; }
        public int health = 0;
    };

    // component holding entity's armor
    class Armor : Component {
        public Armor() { armor = 3; mFamilyId = CFID.ARMOR; }
        public int armor = 3;
    };

    // component holding entity's attack power
    class Attack : Component {
        public Attack() { strength = 2;  mFamilyId = CFID.ATTACK; }
        public int strength = 0;
    }

    // System responsible of creating tanks
    class TankFactory : ComponentSystem {
    
	    public int Create( string name )
        {
            // create new entity
            int eid = Entity.CreateNewEntity();

            // add attack, health, name and armor components to entity
            CreateComponent<Attack>(eid);

            var com_armor = CreateComponent<Armor>(eid);
            var com_health = CreateComponent<Health>(eid);

            // randomize a bit health and armor
            com_health.health = RND.GetRandomNumber(5, 10);
            com_armor.armor += RND.GetRandomNumber(0, 2);

            CreateComponent<Name>(eid).name = name;

            return eid;
        }
    }

    // system responsible of battling  tanks
    class TankBattleSystem : ComponentSystem {
    
	    public bool MakeAttack(int attacker, int defender)
        {

            // get attackers attack ppower and defender's armor
            Attack attack = Get<Attack>(attacker, CFID.ATTACK);
            Armor armor = Get<Armor>(defender, CFID.ARMOR);

            int attack_power = RND.GetRandomNumber(0, 6);

            // make some kind of attack 
            if (attack_power < attack.strength)
            {

                // attack succeeded
                // reduce defender's health
                Health defender_health = Get<Health>(defender, CFID.HEALTH);
                defender_health.health--;

                // print some stat messages
                Console.WriteLine(Get<Name>(attacker, CFID.NAME).name + " reduces " +
                    Get<Name>(defender, CFID.NAME).name + "'s health with 1 damage to " +
                    defender_health.health + " health.");

                // and return true if defender died.
                if (defender_health.health <= 0)
                    return true;
            }
            else
            {
                
                Console.WriteLine(Get<Name>(attacker, CFID.NAME).name + " misses " + Get<Name>(defender, CFID.NAME).name );
            }

            return false;
        }
    }

    class Program
    {
        static void TestECS()
        {
            ComponentSystemTester cis = new ComponentSystemTester();

            cis.Test();
        }
        static void Main(string[] args)
        {
            //TestECS();

            TankFactory tankFactory = new TankFactory();

            // create two tanks
            int tank1 = tankFactory.Create("Sherman");
            int tank2 = tankFactory.Create("Panzer");

            // fetch all components of two tanks based on their enity id
            List<Component> vec_tank1_components = new List<Component>();
            List<Component> vec_tank2_components = new List<Component>();
            tankFactory.GetComponentsByEntity(tank1, ref vec_tank1_components);
            tankFactory.GetComponentsByEntity(tank2, ref vec_tank2_components);

            // instantiate battle system
            TankBattleSystem battleSystem = new TankBattleSystem();

            // and add to it two tanks previously defined
            battleSystem.
                AttachArray( ref vec_tank1_components).
                AttachArray( ref vec_tank2_components);

            // loop a battle between two tanks until one is dead.
            bool battle_ongoing = true;
            while (battle_ongoing)
            {

                // if make attack returns true, then the defender is dead
                if (battleSystem.MakeAttack(tank1, tank2) == false)
                {
                    if (battleSystem.MakeAttack(tank2, tank1))
                    {
                        Console.WriteLine(tankFactory.Get<Name>(tank2, CFID.NAME).name + " wins." );
                        battle_ongoing = false;
                    }
                }
                else
                {
                    Console.WriteLine(tankFactory.Get<Name>(tank1, CFID.NAME).name + " wins." );
                    battle_ongoing = false;
                }
            }
        }
    }
}
