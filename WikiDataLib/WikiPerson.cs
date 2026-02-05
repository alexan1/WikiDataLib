using System;
using System.ComponentModel.DataAnnotations;

namespace WikiDataLib
{
    /// <summary>
    /// Represents a person entity from WikiData.
    /// </summary>
    public class WikiPerson
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy}")]
        public DateTime? Birthday { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy}")]
        public DateTime? Death { get; set; }
        public string? Image { get; set; }
        public string? Link { get; set; }
    }
}
