using System;
using System.Collections.Generic;

namespace Sip.be.Shared.Constants.Documents
{
    /// <summary>
    /// Hằng số cố định của chuẩn đóng gói SIP_tailieu (METS + cấu trúc thư mục).
    /// Theo gói thật uuid-0F203B84... (biến thể E-ARK CSIP). KHÔNG đưa vào DB.
    /// </summary>
    public static class SipPackConstants
    {
        // ----- Phần mềm tạo gói -----
        public const string AgentName = "DongGoiSip";            // <agent>/<name>
        public const string AgentRole = "CREATOR";            // <agent>/@ROLE
        public const string AgentTypeSoftware = "OTHER";      // agent root @TYPE
        public const string AgentOtherType = "SOFTWARE";      // agent root @OTHERTYPE
        public const string AgentTypeIndividual = "INDIVIDUAL"; // agent rep1 @TYPE

        // ----- Namespace METS -----
        public const string NsMets = "http://www.loc.gov/METS/";
        public const string NsXsi = "http://www.w3.org/2001/XMLSchema-instance";
        public const string NsXlink = "http://www.w3.org/1999/xlink";
        public const string NsCsip = "https://DILCIS.eu/XML/METS/CSIPExtensionMETS";
        public const string NsSip = "https://DILCIS.eu/XML/METS/SIPExtensionMETS";

        // xsi:schemaLocation cho METS gói gốc (đường dẫn tới schemas/ trong gói)
        public const string SchemaLocationRoot =
            "http://www.loc.gov/METS/ schemas/mets1_12.xsd " +
            "http://www.w3.org/1999/xlink schemas/xlink.xsd " +
            "https://dilcis.eu/XML/METS/CSIPExtensionMETS schemas/DILCISExtensionMETS.xsd " +
            "https://dilcis.eu/XML/METS/SIPExtensionMETS schemas/DILCISExtensionSIPMETS.xsd";

        // xsi:schemaLocation cho METS bản đại diện (đường dẫn tương đối ../../schemas/)
        public const string SchemaLocationRep =
            "http://www.loc.gov/METS/ ../../schemas/mets1_12.xsd " +
            "http://www.w3.org/1999/xlink ../../schemas/xlink.xsd " +
            "https://dilcis.eu/XML/METS/CSIPExtensionMETS ../../schemas/DILCISExtensionMETS.xsd " +
            "https://dilcis.eu/XML/METS/SIPExtensionMETS ../../schemas/DILCISExtensionSIPMETS.xsd";

        // ----- METS -----
        public const string Profile = "https://earkcsip.dilcis.eu/profile/E-ARK-CSIP.xml";
        public const string MetsTypeRoot = "Mixed";           // mets/@TYPE (gói thật dùng "Mixed")
        public const string ContentInformationType = "MIXED"; // csip:CONTENTINFORMATIONTYPE
        public const string OaisPackageType = "SIP";          // csip:OAISPACKAGETYPE
        public const string RecordStatusDefault = "NEW";      // RECORDSTATUS mặc định
        public const string RepObjid = "rep1";                // OBJID của METS bản đại diện (chuỗi cố định)
        public const string NoteTypeIdentificationCode = "IDENTIFICATIONCODE"; // note/@csip:NOTETYPE (mã phông)
        public const string AltRecordIdType = "SUBMISSIONAGREEMENT";

        // dmdSec / mdRef
        public const string MdType = "DC";                    // mdRef/@MDTYPE
        public const string MdTypeVersion = "1.0.0";          // mdRef/@MDTYPEVERSION
        public const string LocType = "URL";                  // @LOCTYPE
        public const string XlinkTypeSimple = "simple";       // Flocat/@xlink:type
        public const string DmdStatusCurrent = "CURRENT";     // dmdSec/@STATUS

        // file
        public const string ChecksumType = "SHA-256";         // @CHECKSUMTYPE
        public const string MimeTypeXml = "application/xml";  // metadata mime
        public const string MimeTypeOctetStream = "application/octet-stream"; // schema mime

        // Quy tắc ID
        public const string UuidPrefix = "uuid-";             // OBJID/dmdSec/fileGrp/div...
        public const string FileIdPrefix = "ID-";             // <file>/@ID

        // ----- Tên thư mục / file cố định -----
        public const string MetsFileName = "METS.xml";
        public const string EadFileName = "EAD.xml";
        public const string MetadataDir = "metadata";
        public const string DescriptiveDir = "descriptive";
        public const string RepresentationsDir = "representations";
        public const string Rep1Dir = "rep1";
        public const string SchemasDir = "schemas";
        public const string DataDir = "data";
        public const string DocumentationDir = "documentation";

        // structMap fileGrp USE / LABEL
        public const string UseSchemas = "Schemas";
        public const string UseRepresentations = "Representations/rep1";
        public const string UseData = "Data";
        public const string LabelCsip = "CSIP";
        public const string LabelMetadata = "Metadata";
        public const string LabelData = "Data";
        public const string LabelMetadataLink = "MetadataLink";
        public const string LabelMetadataLinkFile = "MetadataLink/File";
        public const string LabelAttachmentFile = "AttachmentFile";

        // Tiền tố tên file metadata tài liệu: EAD_DOC_/EAD_PIC_/EAD_MEDIA_ + <docId>.xml
        public const string EadDocPrefix = "EAD_DOC_";
        public const string EadPicPrefix = "EAD_PIC_";
        public const string EadMediaPrefix = "EAD_MEDIA_";

        // Thư mục template chứa bộ .xsd (nằm trong output, copy từ Sip.be.Shared/Templates/schemas)
        public const string SchemaTemplateDir = "Templates";
        public const string SchemaTemplateSubDir = "schemas";

        // Schema đính kèm gói
        public static readonly string[] SchemaFiles =
        {
            "ead.xsd", "ead_doc.xsd", "ead_pic.xsd", "ead_media.xsd",
            "mets1_12.xsd", "xlink.xsd", "DILCISExtensionMETS.xsd", "DILCISExtensionSIPMETS.xsd"
        };
    }
}
