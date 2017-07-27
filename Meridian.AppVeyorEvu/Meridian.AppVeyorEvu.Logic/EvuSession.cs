﻿// ----------------------------------------------------------------------------
// <copyright file="EvuSession.cs" company="MTCS (Matt Middleton)">
// Copyright (c) Meridian Technology Consulting Services (Matt Middleton).
// All rights reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Meridian.AppVeyorEvu.Logic
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Meridian.AppVeyorEvu.Logic.Definitions;

    /// <summary>
    /// Implements <see cref="IEvuSession" />.
    /// </summary> 
    public class EvuSession : IEvuSession
    {
        /// <summary>
        /// The base URI for the AppVeyor API.
        /// </summary>
        private const string AppVeyorApiBaseUri = 
            "https://ci.appveyor.com/api/";

        /// <summary>
        /// A relative URI to list all environments.
        /// </summary>
        private const string AllEnvironmentsRelUri =
            "./environments";

        /// <summary>
        /// A relative URI to list settings for an environment.
        /// </summary>
        private const string EnvironmentSettingsUri =
            "./environments/{0}/settings";

        /// <summary>
        /// An instance of <see cref="ICsvProvider" />.
        /// </summary>
        private readonly ICsvProvider csvProvider;

        /// <summary>
        /// An instance of <see cref="ILoggingProvider" />.
        /// </summary>
        private readonly ILoggingProvider loggingProvider;

        /// <summary>
        /// Initialises a new instance of the <see cref="EvuSession" /> class.
        /// </summary>
        /// <param name="csvProvider">
        /// An instance of <see cref="ICsvProvider" />.
        /// </param>
        /// <param name="loggingProvider">
        /// An instance of <see cref="ILoggingProvider" />.
        /// </param>
        public EvuSession(
            ICsvProvider csvProvider,
            ILoggingProvider loggingProvider)
        {
            this.csvProvider = csvProvider;
            this.loggingProvider = loggingProvider;
        }

        /// <summary>
        /// Implements <see cref="IEvuSession.Run()" />.
        /// </summary> 
        /// <param name="apiToken">
        /// The AppVeyor API token to be used.
        /// </param>
        /// <param name="environments">
        /// A list of environment names, as they appear in AppVeyor.
        /// </param>
        /// <param name="outputCsvLocation">
        /// The destination location for the CSV file, as a
        /// <see cref="FileInfo" /> instance.
        /// </param>
        /// <returns>
        /// Returns true if the process completed with success, otherwise
        /// false.
        /// </returns>
        public bool CompareEnvironmentVariables(
            string apiToken,
            string[] environments,
            FileInfo outputCsvLocation)
        {
            bool toReturn = default(bool);

            // TODO: Validation/error handling on opitional options.
            this.loggingProvider.Debug(
                "Attempting to pull back all environments...");

            Models.Environment[] allEnvironments =
                this.ExecuteAppVeyorApi<Models.Environment[]>(
                    apiToken,
                    new Uri(AllEnvironmentsRelUri, UriKind.Relative));

            this.loggingProvider.Info(
                $"{allEnvironments.Length} environment(s) returned.");

            this.loggingProvider.Debug("Environments returned:");

            foreach (Models.Environment environment in allEnvironments)
            {
                this.loggingProvider.Debug($"-> {environment}");
            }

            string environmentsPassedIn = string.Join(
                ", ",
                environments.Select(x => $"\"{x}\""));

            this.loggingProvider.Debug(
                $"Selecting environment names passed in " +
                $"({environmentsPassedIn}) from the ones pulled back from " +
                $"the API...");

            Models.Environment[] matchingEnvs = allEnvironments
                .Where(x => environments.Contains(x.Name))
                .ToArray();

            if (environments.Length != matchingEnvs.Length)
            {
                string[] notMatched = environments
                    .Except(matchingEnvs.Select(y => y.Name))
                    .ToArray();

                string notMatchedDesc = string.Join(
                    ", ",
                    notMatched.Select(x => $"\"{x}\""));

                this.loggingProvider.Warn(
                    $"Warning! Could not find more than one environment " +
                    $"passed in! Environments not found: {notMatchedDesc}. " +
                    $"Execution will continue with found environments.");
            }

            // Pull back the environment variables for each of the matching
            // environments.
            Models.EnvironmentDetail[] envSettings = matchingEnvs
                .Select(x => this.PullBackEnvironmentSettings(apiToken, x))
                .ToArray();

            List<string[]> rowsList = new List<string[]>();

            // First, write all the column headers. This will be the
            // environment names. Remember to include an empty first header.
            string[] columnHeaders = new string[] { " " }
                .Concat(envSettings.Select(x => x.Name))
                .ToArray();
            rowsList.Add(columnHeaders);

            // Next, get a list of all the environment variables used.
            // These will be the row headers.
            string[] environmentVarNames = envSettings
                .SelectMany(x => x.Settings
                    .EnvironmentVariables
                    .Select(y => y.Name))
                .Distinct()
                .OrderBy(x => x) // May as well have them in alphabetical order.
                .ToArray();

            // Now, write each setting out, and an environment value, if
            // required.
            // TODO: Needs a refactor.
            string[][] csvContent = environmentVarNames
                .Select((x) =>
                {
                    List<string> rowBodyContent = new List<string>();

                    // Row header here.
                    rowBodyContent.Add(x);

                    // Then each environment's value (if it exists, of course).
                    string[] varRow = columnHeaders
                        .Where(y => !string.IsNullOrWhiteSpace(y))
                        .Select(y =>
                        {
                            string envVarValue = null;

                            Models.EnvironmentDetail environmentDetail =
                                envSettings.Single(z => z.Name == y);

                            envVarValue = environmentDetail.Settings
                                .EnvironmentVariables
                                .Where(z => z.Name == x)
                                .Select(z => z.Value.Value)
                                .SingleOrDefault();

                            return envVarValue;
                        })
                        .ToArray();

                    rowBodyContent.AddRange(varRow);

                    return rowBodyContent.ToArray();
                })
                .ToArray();

            rowsList.AddRange(csvContent);

            this.csvProvider.WriteCsv(
                outputCsvLocation,
                rowsList.ToArray());
                
            return toReturn;
        }

        /// <summary>
        /// Executes a <paramref name="methodEndpoint" /> against the AppVeyor
        /// API.
        /// </summary>
        /// <typeparam name="ResultType">
        /// A model type representing the returned JSON.
        /// </typeparam>
        /// <param name="apiToken">
        /// The AppVeyor API token to be used.
        /// </param>
        /// <param name="methodEndpoint">
        /// The method endpoint to invoke.
        /// </param>
        /// <returns>
        /// The result as a <typeparamref name="ResultType" /> instance.
        /// </returns>
        private ResultType ExecuteAppVeyorApi<ResultType>(
            string apiToken,
            Uri methodEndpoint)
            where ResultType : class
        {
            ResultType toReturn = null;

            using (HttpClient httpClient = new HttpClient())
            {
                // Setup headers.
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiToken);

                Uri apiPath = new Uri(
                    new Uri(AppVeyorApiBaseUri),
                    methodEndpoint);

                this.loggingProvider.Debug($"Invoking {apiPath}...");

                // Get the list of roles
                Task<HttpResponseMessage> getTask =
                    httpClient.GetAsync(apiPath);

                using (HttpResponseMessage response = getTask.Result)
                {
                    this.loggingProvider.Debug(
                        $"HTTP response code returned: " +
                        $"{response.StatusCode}.");

                    response.EnsureSuccessStatusCode();

                    this.loggingProvider.Debug(
                        $"Reading results as {typeof(ResultType).Name}...");

                    Task<ResultType> readAsTask =
                        response.Content.ReadAsAsync<ResultType>();

                    this.loggingProvider.Debug("Results parsed with success.");

                    toReturn = readAsTask.Result;
                }
            }

            return toReturn;
        }

        /// <summary>
        /// Pulls back the environment settings for an individual
        /// <see cref="Models.Environment" /> from the AppVeyor API.
        /// </summary>
        /// <param name="apiToken">
        /// The AppVeyor API token to be used.
        /// </param>
        /// <param name="environment">
        /// An <see cref="Models.Environment" /> instance.
        /// </param>
        /// <returns>
        /// A more fully populated <see cref="Models.EnvironmentDetail" />
        /// instance.
        /// </returns>
        private Models.EnvironmentDetail PullBackEnvironmentSettings(
            string apiToken,
            Models.Environment environment)
        {
            Models.EnvironmentDetail toReturn = null;

            string uriStr = string.Format(
                EnvironmentSettingsUri,
                environment.DeploymentEnvironmentId);

            Models.SingleEnvironmentWrapper singleEnvironment =
                this.ExecuteAppVeyorApi<Models.SingleEnvironmentWrapper>(
                    apiToken,
                    new Uri(uriStr, UriKind.Relative));

            toReturn = singleEnvironment.Environment;

            return toReturn;
        }
    }
}