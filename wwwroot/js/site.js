// Dark mode toggling
document.addEventListener('DOMContentLoaded', function () {
    // Check for saved theme
    const currentTheme = localStorage.getItem('theme');
    if (currentTheme) {
        document.documentElement.setAttribute('data-bs-theme', currentTheme);
        updateThemeIcon(currentTheme);
        updateKanbanColors(currentTheme);
    }

    // Theme toggle button handler
    const themeToggleBtn = document.getElementById('theme-toggle');
    if (themeToggleBtn) {
        themeToggleBtn.addEventListener('click', function () {
            const currentTheme = document.documentElement.getAttribute('data-bs-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

            document.documentElement.setAttribute('data-bs-theme', newTheme);
            localStorage.setItem('theme', newTheme);

            updateThemeIcon(newTheme);
            updateKanbanColors(newTheme);
        });
    }
});

function updateThemeIcon(theme) {
    const themeIcon = document.getElementById('theme-icon');
    if (themeIcon) {
        if (theme === 'dark') {
            themeIcon.classList.remove('bi-moon-fill');
            themeIcon.classList.add('bi-sun-fill');
        } else {
            themeIcon.classList.remove('bi-sun-fill');
            themeIcon.classList.add('bi-moon-fill');
        }
    }
}

function updateKanbanColors(theme) {
    // Apply theme-specific styles for Kanban elements
    const isDarkMode = theme === 'dark';
    
    // Update Kanban board and columns
    const kanbanColumns = document.querySelectorAll('.kanban-column');
    kanbanColumns.forEach(column => {
        if (isDarkMode) {
            column.style.backgroundColor = '#2c3034'; // Darker background for columns
            column.style.borderColor = '#495057';
        } else {
            column.style.backgroundColor = '#f8f9fa'; // Original light background
            column.style.borderColor = '#ddd';
        }
    });

    // Update Kanban cards
    const kanbanCards = document.querySelectorAll('.kanban-card');
    kanbanCards.forEach(card => {
        if (isDarkMode) {
            card.style.backgroundColor = '#343a40'; // Darker background for cards
            card.style.borderColor = '#495057';
            card.style.color = '#e9ecef';
        } else {
            card.style.backgroundColor = 'white'; // Original light background
            card.style.borderColor = '#ddd';
            card.style.color = '';
        }
    });

    // Update Kanban card headers and footers
    const cardHeaders = document.querySelectorAll('.kanban-card-header');
    const cardFooters = document.querySelectorAll('.kanban-card-footer');
    cardHeaders.forEach(header => {
        if (isDarkMode) {
            header.style.borderBottomColor = '#495057';
        } else {
            header.style.borderBottomColor = '#ddd';
        }
    });
    cardFooters.forEach(footer => {
        if (isDarkMode) {
            footer.style.borderTopColor = '#495057';
        } else {
            footer.style.borderTopColor = '#eee';
        }
    });

    // Keep column header colors (they have specific colors set by the user)
    // But adjust text color for better visibility
    const columnHeaders = document.querySelectorAll('.kanban-column-header');
    columnHeaders.forEach(header => {
        // Get the background color
        const bgColor = getComputedStyle(header).backgroundColor;
        
        // Make text white for dark backgrounds, black for light backgrounds
        // This uses a simple brightness calculation
        if (isDarkMode || isColorDark(bgColor)) {
            header.style.color = 'white';
        } else {
            header.style.color = 'black';
        }
    });
}

// Helper function to detect if a color is dark
function isColorDark(color) {
    // Extract RGB components
    let r, g, b;
    
    if (color.startsWith('rgb')) {
        // For rgb(r, g, b) format
        const match = color.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*[\d.]+)?\)/);
        if (!match) return false;
        
        r = parseInt(match[1]);
        g = parseInt(match[2]);
        b = parseInt(match[3]);
    } else if (color.startsWith('#')) {
        // For hex format #RRGGBB
        color = color.substring(1); // Remove #
        r = parseInt(color.substring(0, 2), 16);
        g = parseInt(color.substring(2, 4), 16);
        b = parseInt(color.substring(4, 6), 16);
    } else {
        // Default for unknown formats
        return false;
    }
    
    // Calculate perceived brightness (simple formula)
    // The higher the result, the brighter the color
    const brightness = (r * 299 + g * 587 + b * 114) / 1000;
    
    return brightness < 128; // Below 128 is considered dark
}

// Call the updateKanbanColors when the DOM content is loaded and when the page is fully loaded
document.addEventListener('DOMContentLoaded', function() {
    const theme = document.documentElement.getAttribute('data-bs-theme');
    updateKanbanColors(theme);
});

window.addEventListener('load', function() {
    const theme = document.documentElement.getAttribute('data-bs-theme');
    updateKanbanColors(theme);
});