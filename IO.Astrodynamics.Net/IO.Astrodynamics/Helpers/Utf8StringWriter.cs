// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.IO;
using System.Text;

namespace IO.Astrodynamics.Helpers;

public class Utf8StringWriter : StringWriter
{
    public override Encoding Encoding => Encoding.UTF8;
}