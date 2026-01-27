window.scrollToElement = function(elementId) {
    const element = document.querySelector(`[data-element-id="${elementId}"]`);
    if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};
