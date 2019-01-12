using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECS
{
    public class Component
    {
        public int    mUniqueId = 0;
        public int    mEntityId = 0;
        public int    mFamilyId = 0;
        public bool   MarkedForDeletion = false;

        public bool   Valid() { return MarkedForDeletion == false; }

        public Component()
        {

        }
    }

    // std::vector type Resize
    public static class ListExt
    {
        public static void Resize<T>(this List<T> list, int sz, T c = default(T))
        {
            int cur = list.Count;
            if (sz < cur)
                list.RemoveRange(sz, cur - sz);
            else if (sz > cur)
                list.AddRange(Enumerable.Repeat(c, sz - cur));
        }

        public static void PopBack<T>(this List<T> list)
        {
            list.RemoveAt(list.Count - 1);
        }
        public static void Erase<T>(this List<T> list, int ndx)
        {
            list.RemoveAt(ndx);
        }
        public static T Back<T>(this List<T> list)
        {
            if( list.Any())
                return list[list.Count - 1];

            throw new System.InvalidOperationException("Index is out of range");
        }
    }

    public class ComponentSystem
    {
        protected EntitySystem entitySystem = new EntitySystem();
	    private List<Component> mComponentArray = new List<Component>();
        /// <summary>	List of erased unique identifiers. </summary>
        private LinkedList<int> mErasedIds = new LinkedList<int>();

        // used for faster fetching data based on entity ID and on family ID
        private List<List<Component>> mEntityComponentArray = new List<List<Component>>();
        //component_map mEntityComponentMap;
        private Dictionary<int, List<Component>> mFamilyComponentMap = new Dictionary<int, List<Component>>();
        //private Dictionary<>

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Default constructor. </summary>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public ComponentSystem()
        {
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Destructor. </summary>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        ~ComponentSystem()
        {
            mComponentArray.Clear();
            mErasedIds.Clear();
            //mEntityComponentMap.clear();
            mEntityComponentArray.Clear();
            mFamilyComponentMap.Clear();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Creates new entity under specific identifier. Currently used for loading. </summary>
        /// <param name="entityId">	Identifier for the entity. </param>
        /// <returns>	The new new entity under identifier. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        protected int CreateNewEntityUnderId(int entityId )
        {
            ValidateEntity(entityId);

            return entitySystem.CreateNewEntityUnderId(entityId);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	
        /// 		Attaches new given component to component system. If there was a previously deleted
        /// 		object, deleted objects space will be reused, and deleted object's unique Id attached to 
        /// 		newly created component.
        /// </summary>
        /// <param name="newComponent">	[in] If non-null, the new component. </param>
        /// <returns>	Attached component. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        public T CreateComponent<T>( int entityId ) where T : Component
        {
            ValidateEntity(entityId);

            if (mErasedIds.Count == 0 )
            {
                T newComponent = (T)Activator.CreateInstance(typeof(T));

                // no. put new component into the system
                newComponent.mEntityId = entityId;
                mComponentArray.Add(newComponent);

                newComponent.mUniqueId = mComponentArray.Count - 1;

                // map to entity components
                AddToComponentArray(mComponentArray.Last());

                return (T)mComponentArray[mComponentArray.Count - 1];
            }
            
            // yes. get old erased id then replace it with a new component
            int erasedId = mErasedIds.First();
            mErasedIds.RemoveFirst();
            return (T)Replace<T>(erasedId, entityId);
        }
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Attach the component. </summary>
        /// <param name="component">	The component. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public bool AttachComponent(Component component )
        {
            mComponentArray.Add(component);
            AddToComponentArray(component);
            
            return true;
        }

        private void AddToComponentArray(Component com)
        {
            if (com.mEntityId >= mEntityComponentArray.Count)
                mEntityComponentArray.Resize(com.mEntityId + 1);

            if (mEntityComponentArray[com.mEntityId] == null)
                mEntityComponentArray[com.mEntityId] = new List<Component>();

            mEntityComponentArray[com.mEntityId].Add(com);

            if (ValidateFamily(com.mFamilyId, false) == false)
                mFamilyComponentMap[com.mFamilyId] = new List<Component>();

            mFamilyComponentMap[com.mFamilyId].Add(com);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Attach array of components. </summary>
        /// <param name="componentArray">	Array of components. </param>
        /// <returns>	return self. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public ComponentSystem AttachArray( ref List<Component> componentArray )
        {
            for (int i = 0; i < componentArray.Count; i++)
                AttachComponent(componentArray[i]);

            return this;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	
        /// 		Attaches the given new component to component system. If there was a previously deleted
        /// 		object, deleted objects space will be reused, and deleted object's unique Id attached to 
        /// 		newly created component. Components can be multi attached in one row as:
        /// 			componentSystem
        ///					->	MultiAttach<Vertex>				( new Vertex )
        ///					->	MultiAttach<UV>					( new UV );
        /// </summary>
        /// <param name="newComponent">	[in] If non-null, the new component. </param>
        /// <returns>	This instance. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        /*ComponentSystem MultiAttach( entity_t entityId ) {
            Attach( entityId );
            return this;
        }*/

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 	Replaces the component based on component's unique identifier from component system.
        /// 	Component will be replaced only if ownership of smart pointer is held by one object.
        /// 	Multiple ownerships need to be handled, and components from component system's outer
        /// 	scope need to be erased first for this method to succeed.
        /// </summary>
        /// <typeparam name="typename Type">	Type of the typename type. </typeparam>
        /// <param name="uniqueId">	Unique identifier. </param>
        /// <param name="entityId">	Identifier for the entity. </param>
        ///
        /// <returns>
        /// 	Newly attached component. In case of error empty smart pointer is returned.
        /// </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public Component Replace<Type>( int uniqueId,  int entityId ) where Type : Component
        {
            ValidateEntity(entityId);

            // only allow delete of the pointer in case there is no instance of object	
            if (RefCount(uniqueId) == 0)
            {
                if (uniqueId >= mComponentArray.Count)
                {
                    mComponentArray.Resize(uniqueId + 1);
                }

                Type newComponent = (Type)Activator.CreateInstance(typeof(Type));

                newComponent.mUniqueId = uniqueId;
                newComponent.mEntityId = entityId;

                mComponentArray[uniqueId] = newComponent;

                AddToComponentArray(mComponentArray[uniqueId]);

                return mComponentArray[uniqueId];
            }

            return null;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	
        /// 	Releases the component pointer based on component's unique identifier from component system. 
        /// 	Component will be released if ownership of smart pointer is held by only one object.
        /// 	Multiple ownerships need to be handled, and components from component system's outer scope need to
        /// 	be released first for this method to succeed.
        /// </summary>
        /// <param name="uniqueId">	Unique identifier. </param>
        ///
        /// <returns>	true if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        bool Release(int uniqueId )
        {
            // only allow delete of the pointer in case there is 
            // only one instance of object in each of:
            if (RefCount(uniqueId) == 0)
            {
                int entityId = mComponentArray[uniqueId].mEntityId;
                for (int i = 0; i < mEntityComponentArray[entityId].Count; i++)
                {
                    if (mEntityComponentArray[entityId][i].mUniqueId == uniqueId)
                    {
                        mEntityComponentArray[entityId].Erase(i);
                        break;
                    }
                }

                int familyId = mComponentArray[uniqueId].mFamilyId;
                for (int i = 0; i < mFamilyComponentMap[familyId].Count; i++)
                {
                    if (mFamilyComponentMap[familyId][i].mUniqueId == uniqueId)
                    {
                        mFamilyComponentMap[familyId].Erase(i);
                        break;
                    }
                }
                // clear but don't erase
                ResetComponent(uniqueId);
                mErasedIds.AddLast(uniqueId);
                return true;
            }
/*
#ifdef _DEBUG
            std::cout 
            << "Can't release id " << std::to_string( (_Longlong) uniqueId) << "."
            << "Id " << std::to_string( (_Longlong) uniqueId ) << " have refcount of " << 
            std::to_string( (_Longlong) RefCount(uniqueId) ) << std::endl;
#endif
*/
            return false;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Reference count. </summary>
        /// <param name="uniqueId">	Unique identifier. </param>
        /// <returns>	Returns number of references of given object. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public int RefCount( int uniqueId )
        {
            // check for out of bounds
            if (uniqueId < mComponentArray.Count)
            {
               // TODO: if (mComponentArray[uniqueId])
                    // -3 because of 3 map arrays of storing data for access
                    return 1;// mComponentArray[uniqueId].use_count() - 3;
            }
            return 0;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Gets a component based on its uniqueID. </summary>
        /// <param name="unqiueId">	Unqiue identifier for the component. </param>
        /// <returns>	The component. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public Component GetComponent( int unqiueId )
        {
            if (unqiueId < mComponentArray.Count)
                return mComponentArray[unqiueId];

            return null;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Gets a component list based on entityId. </summary>
        /// <param name="entityId">		 	Unqiue identifier for the entity. </param>
        /// <param name="componentsList">	[out] List of components. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void GetComponentsByEntity( int entityId, ref List<Component> componentsList )
        {
            ValidateEntity(entityId);
            if( entityId < mEntityComponentArray.Count && mEntityComponentArray[entityId] != null )
                componentsList = mEntityComponentArray[entityId];
        }

        public void AppendComponentsByEntity( int entityId, ref List<Component> componentsList )
        {
            ValidateEntity(entityId);

            if (entityId >= mEntityComponentArray.Count)
                mEntityComponentArray.Resize(entityId + 1);

            componentsList.AddRange(mEntityComponentArray[entityId]);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Gets a component list based on components family id. </summary>
        /// <param name="familyId">		 	Unqiue identifier for the family. </param>
        /// <param name="componentsList">	[out] List of components. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void GetComponentsByFamily(int familyId, ref List<Component> componentsList )
        {
            ValidateFamily(familyId);
            componentsList = mFamilyComponentMap[familyId];
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Appends component list based on components family and entity id. </summary>
        /// <param name="entityId">		 	Unqiue identifier for the entity. </param>
        /// <param name="familyId">		 	Unqiue identifier for the family. </param>
        /// <param name="componentsList">	[out] List of components. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void GetComponentsByEntityAndFamily(int entityId, int familyId, ref List<Component> componentsList )
        {

            if (ValidateEntity(entityId, false) && ValidateFamily(familyId, false))
            {
                if (entityId < mEntityComponentArray.Count && mEntityComponentArray[entityId] != null)
                {
                    var list = mEntityComponentArray[entityId].FindAll(com => com.mFamilyId == familyId);
                    componentsList.AddRange(list);
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// 	Appends component list based on components family and entity id from external container.
        /// </summary>
        ///
        /// <param name="container">	 	[in] The container. </param>
        /// <param name="entityId">		 	Unqiue identifier for the entity. </param>
        /// <param name="familyId">		 	Unqiue identifier for the family. </param>
        /// <param name="componentsList">	[out] List of components. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void GetComponentsByEntityAndFamily(ref List<List<Component>> container, int entityId, int familyId, ref List<Component> componentsList )
        {
            if (ValidateEntity(entityId, false) && ValidateFamily(familyId, false))
            {
                /*for (int i = 0; i < container[entityId].Count; i++)
                    if (container[entityId][i].mFamilyId == familyId)
                        componentsList.Add(container[entityId][i]);*/
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Gets components by family and entity. </summary>
        ///
        /// <param name="entityId">		 	Identifier for the entity. </param>
        /// <param name="familyId">		 	Identifier for the family. </param>
        /// <param name="componentsList">	[in,out] List of components. </param>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void GetComponentsByFamilyAndEntity(int entityId, int familyId, ref List<Component> componentsList )
        {
            if (ValidateEntity(entityId, false) && ValidateFamily(familyId, false))
            {
                var list = mFamilyComponentMap[familyId].FindAll(com => com.mEntityId == entityId);
                componentsList.AddRange(list);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Searches for the first component by entity and family. </summary>
        ///
        /// <param name="entityId">	Identifier for the entity. </param>
        /// <param name="familyId">	Identifier for the family. </param>
        ///
        /// <returns>	The found component by entity and family. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public Component FindFirstComponentByEntityAndFamily( int entityId, int familyId )
        {

            if (ValidateEntity(entityId, false) && ValidateFamily(familyId, false))
            {
                if (entityId < mEntityComponentArray.Count)
                {
                    return mEntityComponentArray[entityId].Find(com => com.mFamilyId == familyId);
                }
            }

            return null;
        }

        public Component FindFirstComponentByFamily(int familyId )
        {
            if (mFamilyComponentMap[familyId].Any())  {
                return mFamilyComponentMap[familyId][0];
            }

            return null;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Clears this object to its blank/initial state. </summary>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void Clear()
        {

            mComponentArray.Clear();
            mErasedIds.Clear();
            mEntityComponentArray.Clear();
            mFamilyComponentMap.Clear();

            /// <summary>	The dummy component. Used for return values. </summary>
            entitySystem.Clear();
        }
        
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Gets a first component by its type. </summary>
        ///
        /// <typeparam name="typename Type">	Type of the typename type. </typeparam>
        /// <param name="entityId">	Identifier for the entity. </param>
        /// <param name="familyId">	Identifier for the family. </param>
        ///
        /// <returns>	. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public Type Get<Type>( int entityId,  int familyId ) where Type : Component
        {
            Component ptr = FindFirstComponentByEntityAndFamily(entityId, familyId);

            if (ptr != null)
                return (Type)(ptr);

            return null;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Rebuild erased ids. </summary>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public void RebuildErasedIDs()
        {
            mErasedIds.Clear();
            for (int i = 1; i < Size(); i++)
            {
                if (mComponentArray[i].Valid() == false )    // or use_count() == 0 ?
                    mErasedIds.AddLast(i);
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Gets the size of components. </summary>
        /// <returns>	. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public int Size()
        {
            return mComponentArray.Count;
        }

        public int EntitySize()
        {
            return entitySystem.Size();
        }

        public int ErasedIDSize()
        {
            return mErasedIds.Count;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Count components by entity and family. </summary>
        ///
        /// <param name="entityId">	Identifier for the entity. </param>
        /// <param name="familyId">	Identifier for the family. </param>
        ///
        /// <returns>	The total number of components by entity and family. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        public int CountComponentsByEntityAndFamily(int entityId, int familyId)
        {
            int size = 0;
            if (ValidateEntity(entityId, false) && ValidateFamily(familyId, false))
            {
                for (int i = 0; i < mEntityComponentArray[entityId].Count; i++)
                    if (mEntityComponentArray[entityId][i].mFamilyId == familyId)
                        size++;
            }
            return size;
        }

        public bool DeleteComponent(int componentId )
        {
            if (componentId < mComponentArray.Count)
            {
                int familyID = mComponentArray[componentId].mFamilyId;

                if (familyID == 0)
                    return false;

                int entityID = mComponentArray[componentId].mEntityId;
                int uniqueID = mComponentArray[componentId].mUniqueId;

                if (ResetComponent(componentId))
                {
                    if (mFamilyComponentMap.ContainsKey(familyID))
                    {
                        mFamilyComponentMap[familyID].RemoveAll(com => com.mUniqueId == uniqueID);

                        if (mFamilyComponentMap[familyID].Count == 0)
                            mFamilyComponentMap.Remove(familyID);
                    }

                    // remove all occurences of component in entity list
                    if (entityID < mEntityComponentArray.Count && mEntityComponentArray[entityID] != null)
                    {
                        mEntityComponentArray[entityID].RemoveAll( com => com.mUniqueId == uniqueID);
                    }

                    if (componentId == mComponentArray.Count - 1)
                        mComponentArray.PopBack();
                    else
                        mErasedIds.AddLast(componentId);
                }
                
                // erase components downwards, to reduce array length
                while (mComponentArray.Count > 0 && mComponentArray.Back().Valid() == false )
                {
                    // last element should be deleted from component array
                    // delete it from erased id-s also
                    // NOTE: that this line will be effectively executed only if we have at least two iterations
                    mErasedIds.Remove(mComponentArray.Back().mUniqueId);

                    int eid = mComponentArray.Back().mEntityId;

                    // delete all deleteable components from entity-component array
                    if (eid < mEntityComponentArray.Count && mEntityComponentArray[eid] != null)
                    {
                        mEntityComponentArray[eid].RemoveAll(com => com.MarkedForDeletion);
                    }

                    // remove element itself
                    mComponentArray.PopBack();
                }

                // delete obsolete entities
                while (mEntityComponentArray.Count > 0)
                {
                    int length = mEntityComponentArray.Count - 1;

                    if ((mEntityComponentArray[length] != null && mEntityComponentArray[length].Count == 0) || (mEntityComponentArray[length] == null))
                        mEntityComponentArray.Erase(length);
                    else
                        break;
                }
                // since family list container is dictionary, we don't need another check
            }
            return true;
        }
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        /// <summary>	Deletes the given entity based on entityId. </summary>
        ///
        /// <param name="entityId">	The entity identifier to delete. </param>
        ///
        /// <returns>	true if it succeeds, false if it fails. </returns>
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        bool DeleteEntity(int entityId )
        {
            // NOTE: yet to check
            ValidateEntity(entityId);

            if (entitySystem.Delete(entityId))
            {
                List<Component> components = new List<Component>();
                GetComponentsByEntity(entityId, ref components);

                // erase from family map
                for (int i = 0; i < components.Count; i++)
                {
                    mErasedIds.AddLast(components[i].mUniqueId);

                    int componentFamily = components[i].mFamilyId;
                    int uniqueId        = components[i].mUniqueId;

                    for (int u = 0; u < mFamilyComponentMap[componentFamily].Count; u++)
                    {
                        if (mFamilyComponentMap[componentFamily][u].mUniqueId == uniqueId)
                            mFamilyComponentMap[componentFamily].Erase(i);
                    }

                    if (mFamilyComponentMap[componentFamily].Any() == false)
                        mFamilyComponentMap.Remove(componentFamily);
                }

                mEntityComponentArray[entityId].Clear();

                // reset all components
                for (int i = 0; i < components.Count; i++)
                    ResetComponent(components[i].mUniqueId);

                // check if last items are erased, if so, reduce array size
                while (mComponentArray.Any() == false || (mComponentArray.Last().Valid() == false))
                {
                    int lastId = mComponentArray.Count - 1;
                    mComponentArray.PopBack();

                    // also check if there is such a UID in erased ID's list
                    for (var erasedId = mErasedIds.First; erasedId != null; erasedId = erasedId.Next)
                    {
                        if (erasedId.Value == lastId)
                        {
                            mErasedIds.Remove(erasedId);
                            break;
                        }
                    }
                }

                return true;
            }

            return false;
        }

        
	    protected T DuplicateComponent<T>(int newEntityId, ref T sourceComponent) where T : Component
        {
            // NOTE: yet to check
            ValidateEntity(newEntityId);

            T ptrCreatedComponent = CreateComponent<T>(newEntityId);

            T ptrOriginalType = sourceComponent;
            T ptrCopiedType = ptrCreatedComponent;

            int uniqueId = ptrCreatedComponent.mUniqueId;
            int entityId = ptrCreatedComponent.mEntityId;

            ptrCopiedType = ptrOriginalType;

            // HACK: assign operator will overwrite ALL hierarchy data
            // we just put back two item of component which must be unique for 
            // duplicated object
            ptrCreatedComponent.mUniqueId = uniqueId;
            ptrCreatedComponent.mEntityId = entityId;

            return ptrCreatedComponent;
        }

        private bool ResetComponent( int uniqueId )
        {
            if (0 < uniqueId && uniqueId < mComponentArray.Count && mComponentArray[uniqueId].Valid() )
            {
                //Console.Write("Removing uid #" + uniqueId + ", eid #" + mComponentArray[uniqueId].mEntityId + ", fid #" + mComponentArray[uniqueId].mFamilyId + "\n");
                //mComponentArray.Erase(uniqueId);
                mComponentArray[uniqueId].MarkedForDeletion = true;
                return true;
            }

            return false;
        }

        private bool ValidateEntity( int entityId, bool throwException = true )
        {
            if (entityId <= 0)
            {
                if( throwException)
                    throw new System.InvalidOperationException("Entity out of bounds");

                return false;
            }

            return true;
        }

        private bool ValidateFamily(int familyId, bool throwException = true)
        {
            if (mFamilyComponentMap.ContainsKey(familyId) == false)
            {
                if (throwException)
                    throw new System.InvalidOperationException("Entity must be positive number");

                return false;
            }

            return true;
        }

        public bool Validate()
        {
            // check if deleted component is inside erased array
            for( int i = 1; i< mComponentArray.Count; i++)   {
                if( mComponentArray[i].Valid() == false )
                {
                    if (mErasedIds.Contains(i) == false)
                        throw new System.InvalidOperationException("#1");
                }

                // index is actually mUniqueId
                if( mComponentArray[i].mUniqueId != i )
                    throw new System.InvalidOperationException("#1");
            }

            // check if erased array contains elements that are out of bounds, or are not deleted
            for (var erasedId = mErasedIds.First; erasedId != null; erasedId = erasedId.Next)
            {
                if( erasedId.Value >= mComponentArray.Count )
                    throw new System.InvalidOperationException("#2");
                else
                {
                    if( mComponentArray[erasedId.Value].Valid() )
                        throw new System.InvalidOperationException("#3");
                }
            }

            for (int i = 1; i < mComponentArray.Count; i++)
            {
                if (mComponentArray[i].Valid() == false )
                {
                    int entityId = mComponentArray[i].mEntityId;
                    if (entityId < mEntityComponentArray.Count && mEntityComponentArray[entityId] != null)
                    {
                        for (int u = 0; u < mEntityComponentArray[entityId].Count; u++)
                        {
                            if (mEntityComponentArray[entityId][u].mUniqueId == mComponentArray[i].mUniqueId)
                                throw new System.InvalidOperationException("#4");
                        }
                    }
                }
            }

            // check if components are located under appropriate entityID List
            for (int i = 0; i < mEntityComponentArray.Count; i++)
            {
                if (mEntityComponentArray[i] != null)
                {
                    for (int u = 0; u < mEntityComponentArray[i].Count; u++)
                    {
                        if (mEntityComponentArray[i][u].mEntityId != i)
                            throw new System.InvalidOperationException("#5");
                    }
                }
            }
            // check if there are deleted components under enetity List
            for (int i = 0; i < mEntityComponentArray.Count; i++)
            {
                if (mEntityComponentArray[i] != null)
                {
                    for (int u = 0; u < mEntityComponentArray[i].Count; u++)
                    {
                        if( mEntityComponentArray[i][u].Valid() == false )
                            throw new System.InvalidOperationException("Invalid Entity entry");
                    }
                }
            }

            // check if components with familyId are where they shuldd be
            foreach (KeyValuePair<int,List<Component>> entry in mFamilyComponentMap)
            {
                if( entry.Value != null) {
                    for (int i = 0; i < entry.Value.Count; i++)
                    {
                        if( entry.Value[i].mFamilyId != entry.Key)
                            throw new System.InvalidOperationException("Invalid FamilyID entry");
                    }
                }
            }

            foreach (KeyValuePair<int, List<Component>> entry in mFamilyComponentMap)
            {
                if (entry.Value != null)
                {
                    for (int i = 0; i < entry.Value.Count; i++)
                    {
                        if (entry.Value[i].Valid() == false)
                            throw new System.InvalidOperationException("Invalid FamilyID entry");
                    }
                }
            }

            return true;
        }
    }
}
