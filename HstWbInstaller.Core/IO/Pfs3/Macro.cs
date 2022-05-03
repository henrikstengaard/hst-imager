﻿namespace HstWbInstaller.Core.IO.Pfs3
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Blocks;

    public static class Macro
    {
        /* macros on cachedblocks */
        public static bool IsDirBlock(CachedBlock blk) => blk.blk.id == Constants.DBLKID;
        public static bool IsAnodeBlock(CachedBlock blk) => blk.blk.id == Constants.ABLKID;
        public static bool IsIndexBlock(CachedBlock blk) => blk.blk.id == Constants.IBLKID;
        public static bool IsBitmapBlock(CachedBlock blk) => blk.blk.id == Constants.BMBLKID;
        public static bool IsBitmapIndexBlock(CachedBlock blk) => blk.blk.id == Constants.BMIBLKID;
        public static bool IsDeldir(CachedBlock blk) => blk.blk.id == Constants.DELDIRID;
        public static bool IsSuperBlock(CachedBlock blk) => blk.blk.id == Constants.SBLKID;

        /// <summary>
        /// remove node from any list it's added to. Amiga MinList exec
        /// </summary>
        /// <param name="node"></param>
        /// <param name="g"></param>
        public static void MinRemove(LruCachedBlock node, globaldata g)
        {
            // #define MinRemove(node) Remove((struct Node *)node)
            // remove() removes the node from any list it' added to
            
            g.glob_lrudata.LRUpool.Remove(node);
            g.glob_lrudata.LRUqueue.Remove(node);
        }

        public static void MinRemove(CachedBlock node, globaldata g)
        {
            // #define MinRemove(node) Remove((struct Node *)node)
            // remove() removes the node from any list it' added to
            
            // var volume = g.currentvolume;
            // for (var i = 0; i < volume.anblks.Length; i++)
            // {
            //     volume.anblks[i].Remove(node);
            // }
            // for (var i = 0; i < volume.dirblks.Length; i++)
            // {
            //     volume.dirblks[i].Remove(node);
            // }
            // volume.indexblks.Remove(node);
            // volume.bmblks.Remove(node);
            // volume.superblks.Remove(node);
            // volume.deldirblks.Remove(node);
            // volume.bmindexblks.Remove(node);
        }

        public static void MinRemoveX(CachedBlock node, globaldata g)
        {
            // #define MinRemove(node) Remove((struct Node *)node)
            // remove() removes the node from any list it' added to
            
            var volume = g.currentvolume;
            for (var i = 0; i < volume.anblks.Length; i++)
            {
                volume.anblks[i].Remove(node);
            }
            for (var i = 0; i < volume.dirblks.Length; i++)
            {
                volume.dirblks[i].Remove(node);
            }
            volume.indexblks.Remove(node);
            volume.bmblks.Remove(node);
            volume.superblks.Remove(node);
            volume.deldirblks.Remove(node);
            volume.bmindexblks.Remove(node);
        }
        
        /// <summary>
        /// add node to head of list. Amiga MinList exec
        /// </summary>
        /// <param name="list"></param>
        /// <param name="node"></param>
        /// <typeparam name="T"></typeparam>
        public static void MinAddHead<T>(LinkedList<T> list, T node)
        {
            // #define MinAddHead(list, node)  AddHead((struct List *)(list), (struct Node *)(node))
            list.AddFirst(node);
        }

        public static LinkedListNode<T> HeadOf<T>(LinkedList<T> list)
        {
            // #define HeadOf(list) ((void *)((list)->mlh_Head))
            return list.First;
        }
        
        public static async Task<CachedBlock> GetAnodeBlock(ushort seqnr, globaldata g)
        {
            // #define GetAnodeBlock(a, b) (g->getanodeblock)(a,b)
            // g->getanodeblock = big_GetAnodeBlock;
            return await Init.big_GetAnodeBlock(seqnr, g);
        }
        
        /// <summary>
        /// convert anodenr to subclass with seqnr and offset
        /// </summary>
        /// <param name="anodenr"></param>
        /// <returns></returns>
        public static anodenr SplitAnodenr(uint anodenr)
        {
            // typedef struct
            // {
            //     UWORD seqnr;
            //     UWORD offset;
            // } anodenr_t;
            return new anodenr
            {
                seqnr = (ushort)(anodenr >> 16),
                offset = (ushort)(anodenr & 0xFFFF)
            };
        }

        public static bool InPartition(uint blk, globaldata g)
        {
            return blk >= g.firstblock && blk <= g.lastblock;
        }
        
        public static void Hash(CachedBlock blk, LinkedList<CachedBlock>[] list, int mask)
        {
            // #define Hash(blk, list, mask)                           \
            //             MinAddHead(&list[(blk->blocknr/2)&mask], blk)
            MinAddHead(list[(blk.blocknr / 2) & mask], blk);
        }
        
        /*
         * Hashing macros
         */
        public static void ReHash(CachedBlock blk, LinkedList<CachedBlock>[] list, int mask, globaldata g)
        {
            // #define ReHash(blk, list, mask)                         \
            // {                                                       \
            //     MinRemove(blk);                                     \
            //     MinAddHead(&list[(blk->blocknr/2)&mask], blk);      \
            // }
            
            MinRemove(blk, g);
            MinAddHead(list[(blk.blocknr / 2) & mask], blk);
        }        
        
        public static bool IsEmptyDBlk(CachedBlock blk)
        {
            // #define FIRSTENTRY(blok) ((struct direntry*)((blok)->blk.entries))
            // #define IsEmptyDBlk(blk) (FIRSTENTRY(blk)->next == 0)
            return blk.dirblock.entries.Length == 0;
        }
        
        public static bool IsUpdateNeeded(int rtbf_threshold, globaldata g)
        {
/* checks if update is needed now */
// #define IsUpdateNeeded(rtbf_threshold)                              \
//         ((alloc_data.rtbf_index > rtbf_threshold) ||                    \
//         (g->rootblock->reserved_free < RESFREE_THRESHOLD + 5 + alloc_data.tbf_resneed))         \

            var alloc_data = g.glob_allocdata;
            return ((alloc_data.rtbf_index > rtbf_threshold) ||
                    (g.RootBlock.ReservedFree < Constants.RESFREE_THRESHOLD + 5 + alloc_data.tbf_resneed));
        }
    }
}