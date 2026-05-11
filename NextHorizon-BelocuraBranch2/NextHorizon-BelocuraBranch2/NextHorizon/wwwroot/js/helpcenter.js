document.addEventListener('DOMContentLoaded', () => {
  const searchInput = document.getElementById('helpSearchInput');
  const cards = document.querySelectorAll('.help-card');
  const faqs = document.querySelectorAll('.faq-item');
  const noResults = document.getElementById('noResultsMessage');

  // Headings
  const categoriesHeading = document.querySelector('.help-category-header');
  const faqHeading = document.querySelector('.help-center-faq h2');

  // Carousel functionality
  const carouselLeft = document.getElementById('carouselLeft');
  const carouselRight = document.getElementById('carouselRight');
  const helpCenterGrid = document.getElementById('helpCenterGrid');

  if (carouselLeft && carouselRight && helpCenterGrid) {
    carouselLeft.addEventListener('click', () => {
      helpCenterGrid.scrollBy({ left: -320, behavior: 'smooth' });
    });

    carouselRight.addEventListener('click', () => {
      helpCenterGrid.scrollBy({ left: 320, behavior: 'smooth' });
    });
  }

  // Toggle FAQ answers
  faqs.forEach((faq) => {
    const question = faq.querySelector('.faq-question');
    const answer = faq.querySelector('.faq-answer');

    if (question && answer) {
      answer.style.display = 'none';
      question.addEventListener('click', () => {
        const isOpen = faq.classList.contains('open');
        faqs.forEach((f2) => {
          f2.classList.remove('open');
          const ans = f2.querySelector('.faq-answer');
          if (ans) ans.style.display = 'none';
        });
        if (!isOpen) {
          faq.classList.add('open');
          answer.style.display = 'block';
        }
      });
    }
  });

  if (!searchInput) return;

  searchInput.addEventListener('keyup', function () {
    const value = this.value.toLowerCase();
    let visibleCount = 0;
    const cards = document.querySelectorAll('.help-card');
    const faqs = document.querySelectorAll('.faq-item');

    cards.forEach((card) => {
      const title = (card.getAttribute('data-title') || '').toLowerCase();
      const text = card.textContent.toLowerCase();
      if (title.includes(value) || text.includes(value)) {
        card.style.display = 'flex';
        visibleCount++;
      } else {
        card.style.display = 'none';
      }
    });

    faqs.forEach((faq) => {
      const title = (faq.getAttribute('data-title') || '').toLowerCase();
      const questionText =
        faq.querySelector('.faq-question')?.textContent.toLowerCase() || '';
      if (title.includes(value) || questionText.includes(value)) {
        faq.style.display = 'block';
        visibleCount++;
      } else {
        faq.style.display = 'none';
      }
    });

    if (noResults) {
      noResults.style.display = visibleCount === 0 ? 'block' : 'none';
    }

    if (categoriesHeading)
      categoriesHeading.style.display = visibleCount === 0 ? 'none' : 'block';
    if (faqHeading)
      faqHeading.style.display = visibleCount === 0 ? 'none' : 'block';
  });
});