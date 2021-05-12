﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.ProjectSystem.Query;
using Microsoft.VisualStudio.ProjectSystem.Query.Frameworks;
using Microsoft.VisualStudio.ProjectSystem.Query.ProjectModel;
using Microsoft.VisualStudio.ProjectSystem.Query.ProjectModel.Implementation;
using Microsoft.VisualStudio.ProjectSystem.Query.QueryExecution;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Query
{
    /// <summary>
    /// Handles the creation of <see cref="IPropertyPage"/> instances and populating the requested members.
    /// </summary>
    internal static class PropertyPageDataProducer
    {
        public static IEntityValue CreatePropertyPageValue(IQueryExecutionContext queryExecutionContext, IEntityValue parent, IProjectState cache, QueryProjectPropertiesContext propertiesContext, Rule rule, IPropertyPagePropertiesAvailableStatus requestedProperties)
        {
            Requires.NotNull(parent, nameof(parent));
            Requires.NotNull(rule, nameof(rule));

            var identity = new EntityIdentity(
                ((IEntityWithId)parent).Id,
                createKeys());

            return CreatePropertyPageValue(queryExecutionContext, identity, cache, propertiesContext, rule, requestedProperties);

            IEnumerable<KeyValuePair<string, string>> createKeys()
            {
                yield return new(ProjectModelIdentityKeys.PropertyPageName, rule.Name);

                if (propertiesContext.ItemType is not null)
                {
                    yield return new(ProjectModelIdentityKeys.SourceItemType, propertiesContext.ItemType);
                }

                if (propertiesContext.ItemName is not null)
                {
                    yield return new(ProjectModelIdentityKeys.SourceItemName, propertiesContext.ItemName);
                }
            }
        }

        public static IEntityValue CreatePropertyPageValue(IQueryExecutionContext queryExecutionContext, EntityIdentity id, IProjectState cache, QueryProjectPropertiesContext propertiesContext, Rule rule, IPropertyPagePropertiesAvailableStatus requestedProperties)
        {
            Requires.NotNull(rule, nameof(rule));
            var newPropertyPage = new PropertyPageValue(queryExecutionContext.EntityRuntime, id, new PropertyPagePropertiesAvailableStatus());

            if (requestedProperties.Name)
            {
                newPropertyPage.Name = rule.Name;
            }

            if (requestedProperties.DisplayName)
            {
                newPropertyPage.DisplayName = rule.DisplayName;
            }

            if (requestedProperties.Order)
            {
                newPropertyPage.Order = rule.Order;
            }

            if (requestedProperties.Kind)
            {
                newPropertyPage.Kind = rule.PageTemplate;
            }

            ((IEntityValueFromProvider)newPropertyPage).ProviderState = new ContextAndRuleProviderState(cache, propertiesContext, rule);

            return newPropertyPage;
        }

        public static async Task<IEntityValue?> CreatePropertyPageValueAsync(
            IQueryExecutionContext queryExecutionContext,
            EntityIdentity id,
            IProjectService2 projectService,
            IProjectStateProvider queryCacheProvider,
            QueryProjectPropertiesContext propertiesContext,
            string propertyPageName,
            IPropertyPagePropertiesAvailableStatus requestedProperties)
        {
            if (projectService.GetLoadedProject(propertiesContext.File) is UnconfiguredProject project)
            {
                project.GetQueryDataVersion(out string versionKey, out long versionNumber);
                queryExecutionContext.ReportInputDataVersion(versionKey, versionNumber);

                if (await project.GetProjectLevelPropertyPagesCatalogAsync() is IPropertyPagesCatalog projectCatalog
                    && projectCatalog.GetSchema(propertyPageName) is Rule rule
                    && !rule.PropertyPagesHidden)
                {
                    IProjectState propertyPageQueryCache = queryCacheProvider.CreateCache(project);
                    IEntityValue propertyPageValue = CreatePropertyPageValue(queryExecutionContext, id, propertyPageQueryCache, propertiesContext, rule, requestedProperties);
                    return propertyPageValue;
                }
            }

            return null;
        }

        public static async Task<IEnumerable<IEntityValue>> CreatePropertyPageValuesAsync(
            IQueryExecutionContext queryExecutionContext,
            IEntityValue parent,
            UnconfiguredProject project,
            IProjectStateProvider queryCacheProvider,
            IPropertyPagePropertiesAvailableStatus requestedProperties)
        {
            if (await project.GetProjectLevelPropertyPagesCatalogAsync() is IPropertyPagesCatalog projectCatalog)
            {
                return createPropertyPageValuesAsync();
            }

            return Enumerable.Empty<IEntityValue>();

            IEnumerable<IEntityValue> createPropertyPageValuesAsync()
            {
                IProjectState propertyPageQueryCache = queryCacheProvider.CreateCache(project);
                QueryProjectPropertiesContext propertiesContext = new QueryProjectPropertiesContext(isProjectFile: true, project.FullPath, itemType: null, itemName: null);
                foreach (string schemaName in projectCatalog.GetProjectLevelPropertyPagesSchemas())
                {
                    if (projectCatalog.GetSchema(schemaName) is Rule rule
                        && !rule.PropertyPagesHidden)
                    {
                        IEntityValue propertyPageValue = CreatePropertyPageValue(queryExecutionContext, parent, propertyPageQueryCache, propertiesContext, rule, requestedProperties: requestedProperties);
                        yield return propertyPageValue;
                    }
                }
            }
        }
    }
}
