﻿// ----------------------------------------------------------------------------
// <copyright file="Options.cs" company="MTCS (Matt Middleton)">
// Copyright (c) Meridian Technology Consulting Services (Matt Middleton).
// All rights reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Meridian.AppVeyorEvu.Console
{
    using System.Collections.Generic;
    using System.IO;
    using CommandLine;

    /// <summary>
    /// An options class, used by the <see cref="CommandLine.Parser" /> to
    /// provide option metadata and clean parsing.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// Gets or sets the AppVeyor API token to be used.
        /// </summary>
        [Option(
            HelpText = "An AppVeyor API token.",
            Required = true)]
        public string ApiToken
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a list of environment names, as they appear in
        /// AppVeyor.
        /// </summary>
        [Option(
            HelpText = "A list of AppVeyor environment names.",
            Required = true)]
        public IList<string> Environments
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the location of the output CSV when comparing
        /// environment variables.
        /// </summary>
        [Option(
            HelpText = "The location in which to output environment " +
                "variables when comparing.",
            Required = true)]
        public FileInfo OutputCsvLocation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a logging level to use. Optional - default value is
        /// <see cref="VerbosityOption.Warn" />. 
        /// </summary>
        [Option(
            HelpText = "Specify the verbosity of output from the " +
                "application. Valid options are: \"Debug\", \"Info\", " +
                "\"Warn\", \"Error\", \"Fatal\" or \"Off\".",
            Default = VerbosityOption.Warn)]
        public VerbosityOption Verbosity
        {
            get;
            set;
        }
    }
}