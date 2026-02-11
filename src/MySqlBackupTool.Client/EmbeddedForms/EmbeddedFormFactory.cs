using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace MySqlBackupTool.Client.EmbeddedForms
{
    /// <summary>
    /// Factory for creating embedded form instances
    /// </summary>
    public static class EmbeddedFormFactory
    {
        /// <summary>
        /// Creates an embedded form instance
        /// </summary>
        /// <typeparam name="T">The type of embedded form to create</typeparam>
        /// <param name="serviceProvider">Service provider for dependency injection</param>
        /// <returns>A new instance of the embedded form</returns>
        /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when form creation fails</exception>
        public static T CreateForm<T>(IServiceProvider serviceProvider) 
            where T : UserControl, IEmbeddedForm
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            try
            {
                // Try to resolve from DI container first
                var form = serviceProvider.GetService<T>();
                
                if (form != null)
                {
                    return form;
                }

                // If not registered in DI, try to create with ActivatorUtilities
                form = ActivatorUtilities.CreateInstance<T>(serviceProvider);
                
                if (form == null)
                {
                    throw new InvalidOperationException($"Failed to create instance of {typeof(T).Name}");
                }

                return form;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create embedded form of type {typeof(T).Name}", 
                    ex);
            }
        }
    }
}
