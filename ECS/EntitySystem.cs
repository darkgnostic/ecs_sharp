using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECS
{
    public class EntitySystem
    {
        private List<int> Entities;
        private LinkedList<int> ErasedIds;

        public EntitySystem()
        {
            Entities = new List<int>();
            ErasedIds = new LinkedList<int>();

            Entities.Add(Entities.Count);
        }

        ~EntitySystem()
        {
            Entities.Clear();
            ErasedIds.Clear();
        }

        public int CreateNewEntity()
        {
            //
            if (ErasedIds.Count == 0)
            {
                Entities.Add(Entities.Count);
                return Entities.Last();
            }
            else
            {
                // yes. get old erased id then replace it with a new component
                int erasedId = ErasedIds.First();
                ErasedIds.RemoveFirst();
                Entities[erasedId] = erasedId;
                return erasedId;
            }
        }

        /// <summary>	Creates new entity under specific identifier. Gasps will be reserved and erased.
        /// 			In case entity ID is already reserved, function will fail. </summary>
        public int CreateNewEntityUnderId(int entityId)
        {
            if (entityId > 0)
            {
                if (Exist(entityId))
                    return 0;

                // check under erased ID-s
                if (ErasedIds.Count > 0)
                {
                    // look for entity ID under erased ID-s
                    
                    for (var erasedId = ErasedIds.First; erasedId != null; erasedId = erasedId.Next)
                    {
                        if (erasedId.Value == entityId)
                        {
                            // we have found it
                            ErasedIds.Remove(erasedId);
                            Entities[entityId] = entityId;

                            return entityId;
                        }
                    }
                }

                // we don't have any erased entities or its not found under erased ones
                // entity ID will be next in row?
                if (entityId == Entities.Count)
                {
                    // yes. just create and return normally.
                    Entities.Add(Entities.Count);
                    return Entities.Count;
                }

                // on this point it can't be lesser than size (either its in erased ID's or its took )
                if (entityId < Entities.Count)
                    return 0;

                for (int i = Entities.Count; i < entityId; i++)
                {
                    Entities.Add(i);
                    ErasedIds.AddLast(i);
                }

                Entities.Add(entityId);
                return entityId;
            }

            return 0;
        }
        public int Size()
        {
            return Entities.Count;
        }

        public bool Delete(int entityId)
        {
            if (Exist(entityId))
            {
                if (entityId != Entities.Last())
                    ErasedIds.AddLast(entityId);
                else
                    EraseLast();

                // check if last items are erased, if so, reduce array size
                while (Entities.Any())
                {
                    int lastId = Entities.Last();

                    bool erased = false;
                    // also check if there is such a UID in erased ID's list
                    for (var erasedId = ErasedIds.First; erasedId != null; erasedId = erasedId.Next)
                    {
                        if( erasedId.Value == lastId )
                        {
                            EraseLast();
                            ErasedIds.Remove(erasedId.Value);
                            erased = true;
                            break;
                        }
                    }
                  
                    if (!erased)
                        return true;
                }
                return true;
            }

            return false;
        }

        public bool Exist(int parentId)
        {
            // if size is ok
            if (parentId < Entities.Count)
            {
                // and if id is not under erased ID-s
                if (ErasedIds.Find(parentId) != null)
                    return false;   // it doesn't exist

                // then exists
                return true;
            }

            return false;
        }
        public void Clear()
        {
            ErasedIds.Clear();
            Entities.Clear();
            Entities.Add(0);
        }
        private void EraseLast()
        {
            if( Entities.Any())
            {
                Entities.Remove(Entities.Count - 1);
            }
        }

        public void Test()
        {
            // start state, entities count 1, erased 0
            Debug.Assert(Entities.Count == 1);
            Debug.Assert(ErasedIds.Count == 0);
            
            // create 10 entities (11 total)
            for (var i = 1; i <= 10; i++)
                Debug.Assert(CreateNewEntity() == i);
            
            Delete(Entities.Count - 1); // erase last
            Debug.Assert(ErasedIds.Count == 0); // we still have erased count 0, since last element is deleted
            Debug.Assert(Entities.Count == 10); //   10 total 0->9

            Debug.Assert(CreateNewEntity() == 10);  // create again 10th

            Debug.Assert(Exist(9));  // Check if 9th exist
            Delete(9); // erase 9th entity
            Debug.Assert(Exist(9)==false);  // After deletion shouldn't exist

            Debug.Assert(ErasedIds.Count == 1); // but it is moved to erased id-s
            Debug.Assert(ErasedIds.First() == 9); // it is 9
            Debug.Assert(Entities.Count == 11);  //  still 11

            Delete(10);

            Debug.Assert(ErasedIds.Count == 0); // we have now erased count 0, since last and last-1 were deleted
            Debug.Assert(Entities.Count == 9);  //  still 11

            Clear();

            Debug.Assert(CreateNewEntityUnderId(10) == 10);
            Debug.Assert(Entities.Count == 11); //   0->10
            Debug.Assert(ErasedIds.Count == 9); // we have eid 0 & eid 10 here only
            Delete(Entities.Count - 1); // erase last
            Debug.Assert(ErasedIds.Count == 0); // we have only eid 0 here
            Debug.Assert(Entities.Count == 1); // 

        }
    }
}
