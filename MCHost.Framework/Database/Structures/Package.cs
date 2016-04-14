using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public class Package
    {
        [Required]
        [RegularExpression(@"^[a-zA-Z0-9 .,#!@_\-]{3,64}$")]
        public string Name { get; set; }

        public string Description { get; set; }

        //[Required] (not provided)
        [RegularExpression(@"^[a-zA-Z0-9 ._\-]{4,255}$")]
        public string Filename { get; set; }

        public static Package FromResult(QueryResult result)
        {
            return new Package()
            {
                Name = result.GetString(0),
                Description = result.GetString(1),
                Filename = result.GetString(2)
            };
        }
    }
}
