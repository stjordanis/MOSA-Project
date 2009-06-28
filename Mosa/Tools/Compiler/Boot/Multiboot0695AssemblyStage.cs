﻿/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Michael Ruck (<mailto:sharpos@michaelruck.de>)
 *  Kai P. Reisert (<mailto:kpreisert@googlemail.com>)
 */

using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;

using Mosa.Runtime.CompilerFramework;
using Mosa.Runtime.Linker;

using NDesk.Options;
using Mosa.Runtime.Linker.Elf32;
using Mosa.Runtime.Linker.Elf64;

namespace Mosa.Tools.Compiler.Boot
{
    /*
     * FIXME:
     * - Allow video mode options to be controlled by the command line
     * - Allow the specification of additional load modules on the command line
     * - Write the multiboot compliant entry point, which parses the the boot
     *   information structure and populates the appropriate fields in the 
     *   KernelBoot entry.
     */

    /// <summary>
    /// Writes a multiboot v0.6.95 header into the generated binary.
    /// </summary>
    /// <remarks>
    /// This assembly compiler stage writes a multiboot header into the
    /// the data section of the binary file and also creates a multiboot
    /// compliant entry point into the binary.<para/>
    /// The header and entry point written by this stage is compliant with
    /// the specification at 
    /// http://www.gnu.org/software/grub/manual/multiboot/multiboot.html.
    /// </remarks>
    public sealed class Multiboot0695AssemblyStage : IAssemblyCompilerStage, IHasOptions
    {
        #region Constants

        /// <summary>
        /// Magic value in the multiboot header.
        /// </summary>
        private const uint HEADER_MB_MAGIC = 0x1BADB002U;

        /// <summary>
        /// Multiboot flag, which indicates that additional modules must be
        /// loaded on page boundaries.
        /// </summary>
        private const uint HEADER_MB_FLAG_MODULES_PAGE_ALIGNED = 0x00000001U;

        /// <summary>
        /// Multiboot flag, which indicates if memory information must be
        /// provided in the boot information structure.
        /// </summary>
        private const uint HEADER_MB_FLAG_MEMORY_INFO_REQUIRED = 0x00000002U;

        /// <summary>
        /// Multiboot flag, which indicates that the supported video mode
        /// table must be provided in the boot information structure.
        /// </summary>
        private const uint HEADER_MB_FLAG_VIDEO_MODES_REQUIRED = 0x00000004U;

        /// <summary>
        /// Multiboot flag, which indicates a non-elf binary to boot and that
        /// settings for the executable file should be read from the boot header
        /// instead of the executable header.
        /// </summary>
        private const uint HEADER_MB_FLAG_NON_ELF_BINARY = 0x00010000U;

        #endregion // Constants

        #region Data members

        /// <summary>
        /// Holds the multiboot video mode.
        /// </summary>
        private uint videoMode;

        /// <summary>
        /// Holds the videoWidth of the screen for the video mode.
        /// </summary>
        private uint videoWidth;

        /// <summary>
        /// Holds the height of the screen for the video mode.
        /// </summary>
        private uint videoHeight;

        /// <summary>
        /// Holds the depth of the video mode in bits per pixel.
        /// </summary>
        private uint videoDepth;

        #endregion // Data members

        #region Construction

        /// <summary>
        /// Initializes a new instance of the <see cref="Multiboot0695AssemblyStage"/> class.
        /// </summary>
        public Multiboot0695AssemblyStage()
        {
            videoMode = 1;
            videoWidth = 80;
            videoHeight = 25;
            videoDepth = 0;
        }

        #endregion // Construction

        #region IAssemblyCompilerStage Members

        /// <summary>
        /// Retrieves the name of the compilation stage.
        /// </summary>
        /// <value>The name of the compilation stage.</value>
        public string Name
        {
            get { return @"MultibootAssemblyStage"; }
        }

        /// <summary>
        /// Performs stage specific processing on the compiler context.
        /// </summary>
        /// <param name="compiler">The compiler context to perform processing in.</param>
        public void Run(AssemblyCompiler compiler)
        {
            if (compiler == null)
                throw new ArgumentNullException(@"compiler");

            IAssemblyLinker linker = compiler.Pipeline.Find<IAssemblyLinker>();
            Debug.Assert(linker != null, @"No linker??");

            IntPtr entryPoint = WriteMultibootEntryPoint(linker);
            WriteMultibootHeader(compiler, linker, entryPoint);
        }

        #endregion // IAssemblyCompilerStage Members

        #region Internals

        /// <summary>
        /// Writes the multiboot entry point.
        /// </summary>
        /// <param name="linker">The linker.</param>
        /// <returns>The virtualAddress of the real entry point.</returns>
        private IntPtr WriteMultibootEntryPoint(IAssemblyLinker linker)
        {
            /*
             * FIXME:
             * 
             * We can't use the standard entry point of the module. Instead
             * we write a multiboot compliant entry point here, which populates
             * the boot structure - so that it can be retrieved later.
             * 
             * Unfortunately this means, we need to define the boot structure
             * in the kernel to be able to access it later.
             * 
             */

            return IntPtr.Zero;
        }

        private const string MultibootHeaderSymbolName = @"<$>mosa-multiboot-header";

        /// <summary>
        /// Writes the multiboot header.
        /// </summary>
        /// <param name="compiler">The assembly compiler.</param>
        /// <param name="linker">The linker.</param>
        /// <param name="entryPoint">The virtualAddress of the multiboot compliant entry point.</param>
        private void WriteMultibootHeader(AssemblyCompiler compiler, IAssemblyLinker linker, IntPtr entryPoint)
        {
            // HACK: According to the multiboot specification this header must be within the first 8K of the
            // kernel binary. Since the text section is always first, this should take care of the problem.
            using (Stream stream = linker.Allocate(MultibootHeaderSymbolName, SectionKind.Text, 64, 4))
            using (BinaryWriter bw = new BinaryWriter(stream, Encoding.ASCII))
            {
                // flags - multiboot flags
                uint flags = /*HEADER_MB_FLAG_VIDEO_MODES_REQUIRED | */HEADER_MB_FLAG_MEMORY_INFO_REQUIRED | HEADER_MB_FLAG_MODULES_PAGE_ALIGNED;
                // The multiboot header checksum 
                uint csum = 0;
                // header_addr is the load virtualAddress of the multiboot header
                uint header_addr = 0;
                // load_addr is the base virtualAddress of the binary in memory
                uint load_addr = 0;
                // load_end_addr holds the virtualAddress past the last byte to load from the image
                uint load_end_addr = 0;
                // bss_end_addr is the virtualAddress of the last byte to be zeroed out
                uint bss_end_addr = 0;
                // entry_point the load virtualAddress of the entry point to invoke
                uint entry_point = (uint)entryPoint.ToInt32();

                // Are we linking an ELF binary?
                if (!(linker is Elf32Linker || linker is Elf64Linker))
                {
                    // Check the linker layout settings
                    if (linker.LoadSectionAlignment != linker.VirtualSectionAlignment)
                        throw new LinkerException(@"Load and virtual section alignment must be identical if you are booting non-ELF binaries with a multiboot bootloader.");

                    // No, special multiboot treatment required
                    flags |= HEADER_MB_FLAG_NON_ELF_BINARY;

                    header_addr = (uint)linker.Sections.Single((section) =>
                    {
                        return (section.SectionKind == SectionKind.Text);
                    }).VirtualAddress.ToInt64() + (uint)linker.GetSymbol(MultibootHeaderSymbolName).SectionAddress;
                    load_addr = (uint)linker.BaseAddress;
                    load_end_addr = 0;
                    bss_end_addr = 0;
                }

                // Calculate the checksum
                csum = unchecked(0U - HEADER_MB_MAGIC - flags);

                bw.Write(HEADER_MB_MAGIC);
                bw.Write(flags);
                bw.Write(csum);
                bw.Write(header_addr);
                bw.Write(load_addr);
                bw.Write(load_end_addr);
                bw.Write(bss_end_addr);

                linker.Link(LinkType.AbsoluteAddress | LinkType.I4, MultibootHeaderSymbolName, (int)stream.Position, 0, linker.CreateSymbolName(compiler.Assembly.EntryPoint), IntPtr.Zero);
                //bw.Write(entry_point);

                bw.Write(this.videoMode);
                bw.Write(this.videoWidth);
                bw.Write(this.videoHeight);
                bw.Write(this.videoDepth);
            }
        }

        #endregion // Internals
        
        #region IHasOptions Members
        /// <summary>
        /// Adds the additional options for the parsing process to the given OptionSet.
        /// </summary>
        /// <param name="optionSet">A given OptionSet to add the options to.</param>
        public void AddOptions(OptionSet optionSet)
        {
            optionSet.Add(
                "multiboot-video-mode=",
                "Specify the video mode for multiboot [{text|graphics}].",
                delegate(string v)
                {
                    switch (v.ToLower())
                    {
                        case "text":
                            videoMode = 1;
                            break;
                        case "graphics":
                            videoMode = 0;
                            break;
                        default:
                            throw new OptionException("Invalid value for multiboot video mode: " + v, "multiboot-video-mode");
                    }
                });
            
            optionSet.Add(
                "multiboot-video-width=",
                "Specify the {width} for video output, in pixels for graphics mode or in characters for text mode.",
                delegate(string v)
                {
                    uint val;
                    if (uint.TryParse(v, out val))
                    {
                        // TODO: this probably needs further validation
                        videoWidth = val;
                    }
                    else
                    {
                        throw new OptionException("Invalid value for multiboot video width: " + v, "multiboot-video-width");
                    }
                });
            
            optionSet.Add(
                "multiboot-video-height=",
                "Specify the {height} for video output, in pixels for graphics mode or in characters for text mode.",
                delegate(string v)
                {
                    uint val;
                    if (uint.TryParse(v, out val))
                    {
                        // TODO: this probably needs further validation
                        videoHeight = val;
                    }
                    else
                    {
                        throw new OptionException("Invalid value for multiboot video height: " + v, "multiboot-video-height");
                    }
                });
            
            optionSet.Add(
                "multiboot-video-depth=",
                "Specify the {depth} (number of bits per pixel) for graphics mode.",
                delegate(string v)
                {
                    uint val;
                    if (uint.TryParse(v, out val))
                    {
                        // TODO: this probably needs further validation
                        videoDepth = val;
                    }
                    else
                    {
                        throw new OptionException("Invalid value for multiboot video depth: " + v, "multiboot-video-depth");
                    }
                });
            
            optionSet.Add(
                "multiboot-module=",
                "Adds a {0:module} to multiboot, to be loaded at a given {1:virtualAddress} (can be used multiple times).",
                delegate(string file, string address)
                {
                    // TODO: validate and add this to a list or something
                    Console.WriteLine("Adding multiboot module " + file + " at virtualAddress " + address);
                });
        }
        #endregion
    }
}
