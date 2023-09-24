---
name: Feature/Enhancement request
about: Suggest an idea for new feature or changes
title: "[Request]"
labels: 'Enhancement'
body:
    - type: markdown
      attribute:
        value: |
          This is the Feature and/or Enhancement request form for Collapse. Before filling it out, please make sure that there are **no open/closed issues** regarding your request. All text areas support markdown syntax unless explicitly noted otherwise.

    - type: dropdown
      id: is-related
      attribute:
        label: Is your request related to a problem?
        description: This can take the form of an improvement that fixes an issue in Collapse
        options:
          - Yes
          - No
        default: 1
        validations:
          required: true

    - type: textarea
      id: related-desc
      label: How is the feature request related to the problem?
      descrption: If you answered "No" to the previous question, you may skip this field.
      placeholder: Explain how the feature is related to the problem, if it is related.
      validation:
        required: false

    - type: textarea
      id: solution-desc
      label: Describe your proposed solution
      descrption: A clear and concise description of what you want to happen.
      placeholder: Go into as much detail as possible.
      validation:
        required: true

    - type: textarea
      id: alternative-desc
      label: Describe alternatives you've considered
      descrption: A clear and concise description of any alternative solutions or features you've considered.
      placeholder: What are the alternatives you've considered? Sometimes, the Collapse team can't always implement everything the way you envisonned it, so what are some compromises, changes you're willing to make to the current proposal?
      validation:
        required: true

    - type: textarea
      id: additional-context
      label: Additional context
      descrption: Add any other context or screenshots about the feature request here.
      placeholder: If there are any images, concept art, code snippets you're willing to share, please put them here.
      validation:
        required: false