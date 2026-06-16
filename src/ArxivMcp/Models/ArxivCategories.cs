namespace ArxivMcp.Models;

/// <summary>
/// A single arXiv subject classification.
/// </summary>
public class ArxivCategory
{
    public ArxivCategory(string code, string name, string group)
    {
        Code = code;
        Name = name;
        Group = group;
    }

    /// <summary>Category code, e.g. "cs.CL".</summary>
    public string Code { get; set; }

    /// <summary>Human-readable name, e.g. "Computation and Language".</summary>
    public string Name { get; set; }

    /// <summary>Top-level archive group, e.g. "cs", "math", "physics".</summary>
    public string Group { get; set; }
}

/// <summary>
/// Static, offline table of arXiv categories. This does not call the arXiv API and is not
/// subject to the request throttle; it is a fixed reference list used by the list_categories tool.
/// </summary>
public static class ArxivCategories
{
    /// <summary>The full set of arXiv categories, grouped by archive.</summary>
    public static IReadOnlyList<ArxivCategory> All { get; } =
    [
        // Computer Science (cs)
        new("cs.AI", "Artificial Intelligence", "cs"),
        new("cs.AR", "Hardware Architecture", "cs"),
        new("cs.CC", "Computational Complexity", "cs"),
        new("cs.CE", "Computational Engineering, Finance, and Science", "cs"),
        new("cs.CG", "Computational Geometry", "cs"),
        new("cs.CL", "Computation and Language", "cs"),
        new("cs.CR", "Cryptography and Security", "cs"),
        new("cs.CV", "Computer Vision and Pattern Recognition", "cs"),
        new("cs.CY", "Computers and Society", "cs"),
        new("cs.DB", "Databases", "cs"),
        new("cs.DC", "Distributed, Parallel, and Cluster Computing", "cs"),
        new("cs.DL", "Digital Libraries", "cs"),
        new("cs.DM", "Discrete Mathematics", "cs"),
        new("cs.DS", "Data Structures and Algorithms", "cs"),
        new("cs.ET", "Emerging Technologies", "cs"),
        new("cs.FL", "Formal Languages and Automata Theory", "cs"),
        new("cs.GL", "General Literature", "cs"),
        new("cs.GR", "Graphics", "cs"),
        new("cs.GT", "Computer Science and Game Theory", "cs"),
        new("cs.HC", "Human-Computer Interaction", "cs"),
        new("cs.IR", "Information Retrieval", "cs"),
        new("cs.IT", "Information Theory", "cs"),
        new("cs.LG", "Machine Learning", "cs"),
        new("cs.LO", "Logic in Computer Science", "cs"),
        new("cs.MA", "Multiagent Systems", "cs"),
        new("cs.MM", "Multimedia", "cs"),
        new("cs.MS", "Mathematical Software", "cs"),
        new("cs.NA", "Numerical Analysis", "cs"),
        new("cs.NE", "Neural and Evolutionary Computing", "cs"),
        new("cs.NI", "Networking and Internet Architecture", "cs"),
        new("cs.OH", "Other Computer Science", "cs"),
        new("cs.OS", "Operating Systems", "cs"),
        new("cs.PF", "Performance", "cs"),
        new("cs.PL", "Programming Languages", "cs"),
        new("cs.RO", "Robotics", "cs"),
        new("cs.SC", "Symbolic Computation", "cs"),
        new("cs.SD", "Sound", "cs"),
        new("cs.SE", "Software Engineering", "cs"),
        new("cs.SI", "Social and Information Networks", "cs"),
        new("cs.SY", "Systems and Control", "cs"),

        // Mathematics (math)
        new("math.AC", "Commutative Algebra", "math"),
        new("math.AG", "Algebraic Geometry", "math"),
        new("math.AP", "Analysis of PDEs", "math"),
        new("math.AT", "Algebraic Topology", "math"),
        new("math.CA", "Classical Analysis and ODEs", "math"),
        new("math.CO", "Combinatorics", "math"),
        new("math.CT", "Category Theory", "math"),
        new("math.CV", "Complex Variables", "math"),
        new("math.DG", "Differential Geometry", "math"),
        new("math.DS", "Dynamical Systems", "math"),
        new("math.FA", "Functional Analysis", "math"),
        new("math.GM", "General Mathematics", "math"),
        new("math.GN", "General Topology", "math"),
        new("math.GR", "Group Theory", "math"),
        new("math.GT", "Geometric Topology", "math"),
        new("math.HO", "History and Overview", "math"),
        new("math.IT", "Information Theory", "math"),
        new("math.KT", "K-Theory and Homology", "math"),
        new("math.LO", "Logic", "math"),
        new("math.MG", "Metric Geometry", "math"),
        new("math.MP", "Mathematical Physics", "math"),
        new("math.NA", "Numerical Analysis", "math"),
        new("math.NT", "Number Theory", "math"),
        new("math.OA", "Operator Algebras", "math"),
        new("math.OC", "Optimization and Control", "math"),
        new("math.PR", "Probability", "math"),
        new("math.QA", "Quantum Algebra", "math"),
        new("math.RA", "Rings and Algebras", "math"),
        new("math.RT", "Representation Theory", "math"),
        new("math.SG", "Symplectic Geometry", "math"),
        new("math.SP", "Spectral Theory", "math"),
        new("math.ST", "Statistics Theory", "math"),

        // Physics (physics and related archives)
        new("astro-ph.CO", "Cosmology and Nongalactic Astrophysics", "physics"),
        new("astro-ph.EP", "Earth and Planetary Astrophysics", "physics"),
        new("astro-ph.GA", "Astrophysics of Galaxies", "physics"),
        new("astro-ph.HE", "High Energy Astrophysical Phenomena", "physics"),
        new("astro-ph.IM", "Instrumentation and Methods for Astrophysics", "physics"),
        new("astro-ph.SR", "Solar and Stellar Astrophysics", "physics"),
        new("cond-mat.dis-nn", "Disordered Systems and Neural Networks", "physics"),
        new("cond-mat.mes-hall", "Mesoscale and Nanoscale Physics", "physics"),
        new("cond-mat.mtrl-sci", "Materials Science", "physics"),
        new("cond-mat.other", "Other Condensed Matter", "physics"),
        new("cond-mat.quant-gas", "Quantum Gases", "physics"),
        new("cond-mat.soft", "Soft Condensed Matter", "physics"),
        new("cond-mat.stat-mech", "Statistical Mechanics", "physics"),
        new("cond-mat.str-el", "Strongly Correlated Electrons", "physics"),
        new("cond-mat.supr-con", "Superconductivity", "physics"),
        new("gr-qc", "General Relativity and Quantum Cosmology", "physics"),
        new("hep-ex", "High Energy Physics - Experiment", "physics"),
        new("hep-lat", "High Energy Physics - Lattice", "physics"),
        new("hep-ph", "High Energy Physics - Phenomenology", "physics"),
        new("hep-th", "High Energy Physics - Theory", "physics"),
        new("math-ph", "Mathematical Physics", "physics"),
        new("nlin.AO", "Adaptation and Self-Organizing Systems", "physics"),
        new("nlin.CD", "Chaotic Dynamics", "physics"),
        new("nlin.CG", "Cellular Automata and Lattice Gases", "physics"),
        new("nlin.PS", "Pattern Formation and Solitons", "physics"),
        new("nlin.SI", "Exactly Solvable and Integrable Systems", "physics"),
        new("nucl-ex", "Nuclear Experiment", "physics"),
        new("nucl-th", "Nuclear Theory", "physics"),
        new("physics.acc-ph", "Accelerator Physics", "physics"),
        new("physics.ao-ph", "Atmospheric and Oceanic Physics", "physics"),
        new("physics.app-ph", "Applied Physics", "physics"),
        new("physics.atom-ph", "Atomic Physics", "physics"),
        new("physics.bio-ph", "Biological Physics", "physics"),
        new("physics.chem-ph", "Chemical Physics", "physics"),
        new("physics.class-ph", "Classical Physics", "physics"),
        new("physics.comp-ph", "Computational Physics", "physics"),
        new("physics.data-an", "Data Analysis, Statistics and Probability", "physics"),
        new("physics.flu-dyn", "Fluid Dynamics", "physics"),
        new("physics.gen-ph", "General Physics", "physics"),
        new("physics.geo-ph", "Geophysics", "physics"),
        new("physics.hist-ph", "History and Philosophy of Physics", "physics"),
        new("physics.ins-det", "Instrumentation and Detectors", "physics"),
        new("physics.med-ph", "Medical Physics", "physics"),
        new("physics.optics", "Optics", "physics"),
        new("physics.plasm-ph", "Plasma Physics", "physics"),
        new("physics.pop-ph", "Popular Physics", "physics"),
        new("physics.soc-ph", "Physics and Society", "physics"),
        new("physics.space-ph", "Space Physics", "physics"),
        new("quant-ph", "Quantum Physics", "physics"),

        // Quantitative Biology (q-bio)
        new("q-bio.BM", "Biomolecules", "q-bio"),
        new("q-bio.CB", "Cell Behavior", "q-bio"),
        new("q-bio.GN", "Genomics", "q-bio"),
        new("q-bio.MN", "Molecular Networks", "q-bio"),
        new("q-bio.NC", "Neurons and Cognition", "q-bio"),
        new("q-bio.OT", "Other Quantitative Biology", "q-bio"),
        new("q-bio.PE", "Populations and Evolution", "q-bio"),
        new("q-bio.QM", "Quantitative Methods", "q-bio"),
        new("q-bio.SC", "Subcellular Processes", "q-bio"),
        new("q-bio.TO", "Tissues and Organs", "q-bio"),

        // Quantitative Finance (q-fin)
        new("q-fin.CP", "Computational Finance", "q-fin"),
        new("q-fin.EC", "Economics", "q-fin"),
        new("q-fin.GN", "General Finance", "q-fin"),
        new("q-fin.MF", "Mathematical Finance", "q-fin"),
        new("q-fin.PM", "Portfolio Management", "q-fin"),
        new("q-fin.PR", "Pricing of Securities", "q-fin"),
        new("q-fin.RM", "Risk Management", "q-fin"),
        new("q-fin.ST", "Statistical Finance", "q-fin"),
        new("q-fin.TR", "Trading and Market Microstructure", "q-fin"),

        // Statistics (stat)
        new("stat.AP", "Applications", "stat"),
        new("stat.CO", "Computation", "stat"),
        new("stat.ME", "Methodology", "stat"),
        new("stat.ML", "Machine Learning", "stat"),
        new("stat.OT", "Other Statistics", "stat"),
        new("stat.TH", "Statistics Theory", "stat"),

        // Electrical Engineering and Systems Science (eess)
        new("eess.AS", "Audio and Speech Processing", "eess"),
        new("eess.IV", "Image and Video Processing", "eess"),
        new("eess.SP", "Signal Processing", "eess"),
        new("eess.SY", "Systems and Control", "eess"),

        // Economics (econ)
        new("econ.EM", "Econometrics", "econ"),
        new("econ.GN", "General Economics", "econ"),
        new("econ.TH", "Theoretical Economics", "econ"),
    ];

    /// <summary>
    /// Returns the categories in <paramref name="group"/> (case-insensitive), or the full table
    /// when <paramref name="group"/> is null or whitespace.
    /// </summary>
    public static IReadOnlyList<ArxivCategory> ByGroup(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return All;

        return All
            .Where(c => string.Equals(c.Group, group.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
