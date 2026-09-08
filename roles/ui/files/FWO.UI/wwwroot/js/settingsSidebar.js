window.filterSettingsSidebar = (navigationId, searchTerm) => {
    const navigation = document.getElementById(navigationId);
    if (!navigation) {
        return;
    }

    const normalizedSearchTerm = searchTerm.trim().toLocaleLowerCase();
    const elements = Array.from(navigation.children);
    const sections = [];
    let currentSection;

    elements.forEach(element => {
        if (element.matches("li") && element.querySelector("h5")) {
            currentSection = { heading: element, items: [] };
            sections.push(currentSection);
        } else if (element.matches("li") && currentSection) {
            currentSection.items.push(element);
        }
    });

    sections.forEach(section => {
        const headingMatches = matchesSearch(section.heading, normalizedSearchTerm);
        section.items.forEach(item => {
            item.hidden = normalizedSearchTerm !== "" && !headingMatches && !matchesSearch(item, normalizedSearchTerm);
        });
        section.heading.hidden = normalizedSearchTerm !== "" && !headingMatches && section.items.every(item => item.hidden);
    });

    elements.filter(element => element.matches("hr"))
        .forEach(separator => separator.hidden = normalizedSearchTerm !== "");
};

function matchesSearch(element, normalizedSearchTerm) {
    return element.textContent.toLocaleLowerCase().includes(normalizedSearchTerm);
}
